// 
//  Author:
//    Marek Habersack grendel@twistedcode.net
// 
//  Copyright (c) 2010, Novell, Inc (http://novell.com/)
// 
//  All rights reserved.
// 
//  Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
// 
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in
//       the documentation and/or other materials provided with the distribution.
//     * Neither the name of Marek Habersack nor names of the contributors may be used to endorse or promote products derived from this software without specific prior written permission.
// 
//  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
//  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
//  LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
//  A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
//  CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
//  EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
//  PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
//  PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
//  LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
//  NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
//  SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Configuration;
using System.Web.Util;

using CaptainHook.Base;
using CaptainHook.GitHub;
using CaptainHook.Mail;
using CaptainHook.Utils;

namespace CaptainHook.Web.Handlers
{
	sealed class GitHubJsonPostHandler : CommonBase, IHttpHandler
	{
		sealed class SenderState
		{
			object ignoresLock = new object ();
			Dictionary <string, bool> ignoredFiles;

			public string CsDataDir;
			public string CommitSourceID;
			public JsonDeserializer Deserializer;

			public void Ignore (string filePath)
			{
				lock (ignoresLock) {
					if (ignoredFiles == null)
						ignoredFiles = new Dictionary<string, bool> (StringComparer.Ordinal);
					if (ignoredFiles.ContainsKey (filePath))
						return;
					ignoredFiles.Add (filePath, true);
				}
			}

			public bool IsIgnored (string filePath)
			{
				lock (ignoresLock) {
					if (ignoredFiles == null || ignoredFiles.Count == 0)
						return false;
					return ignoredFiles.ContainsKey (filePath);
				}
			}
		}

		sealed class SenderWorkerState
		{
			public string WorkItemPath;
			public SenderState State;
		}

		static readonly object senderDirectoryLock = new object ();

		const int INPUT_BUFFER_LENGTH = 4096;

		public bool IsReusable {
			get { return true; }
		}

		public void ProcessRequest (HttpContext context)
		{
			if (context == null) {
				Log (LogSeverity.Error, "No HttpContext.");
				return;
			}

			HttpRequest req = context.Request;
			if (req == null) {
				Log (LogSeverity.Error, "No request.");
				return;
			}

			if (String.Compare ("application/x-www-form-urlencoded", req.ContentType, StringComparison.OrdinalIgnoreCase) != 0) {
				InvalidRequest (context.Response, 415);
				return;
			}

			string path = VirtualPathUtility.GetFileName (req.Path);
			int underscore = path.IndexOf ('_');
			if (underscore == -1 || !path.EndsWith (".github")) {
				Log (LogSeverity.Error, "Invalid URL format. Expected: authid_csid.github.");
				InvalidRequest (context.Response, 400);
			}

			path = path.Substring (0, path.Length - 7);
			string authID = path.Substring (0, underscore);
			string csID = path.Substring (underscore + 1);

			if (String.IsNullOrEmpty (csID)) {
				Log (LogSeverity.Error, "Missing 'csid' parameter in the request (commit source ID)");
				InvalidRequest (context.Response, 400);
				return;
			}

			if (String.IsNullOrEmpty (authID)) {
				Log (LogSeverity.Error, "Missing 'authid' parameter in the request (authentication ID)");
				InvalidRequest (context.Response, 400);
				return;
			}

			string posterAuthID = WebConfigurationManager.AppSettings ["PosterAuthID"];
			if (String.IsNullOrEmpty (posterAuthID)) {
				Log (LogSeverity.Error, "Configuration error: PosterAuthID must be added to the appSettings section with a non-empty value");
				InvalidRequest (context.Response, 500);
				return;
			}

			if (String.Compare (authID, posterAuthID, StringComparison.Ordinal) != 0) {
				Log (LogSeverity.Error, "Invalid authID in request from {0}", req.UserHostAddress);
				InvalidRequest (context.Response, 403);
				return;
			}

			if (!Config.Instance.IsKnownCommitSource (csID)) {
				Log (LogSeverity.Error, "Commit source ID '{0}' is unknown.", csID);
				InvalidRequest (context.Response, 403);
				return;
			}

			StoreDataAndNotify (context, req, csID);
		}

		void StoreDataAndNotify (HttpContext context, HttpRequest req, string commitSourceID)
		{
			string csDataDir = Path.Combine (Path.Combine (Config.Instance.RootPath, "data"), commitSourceID);

			if (!Directory.Exists (csDataDir)) {
				try {
					Directory.CreateDirectory (csDataDir);
				} catch (Exception ex) {
					Log (ex, "Failed to create data directory '{0}'", csDataDir);
					InvalidRequest (context.Response, 500);
					return;
				}
			}

			DateTime now = DateTime.Now;
			string fileName = Path.Combine (csDataDir, now.Ticks.ToString () + ".json");
			try {
				using (var writer = new FileStream (fileName, FileMode.Create, FileAccess.Write, FileShare.None)) {
					byte[] data = new byte[INPUT_BUFFER_LENGTH];
					Stream input = req.InputStream;
					int rlen;
					while ((rlen = input.Read (data, 0, INPUT_BUFFER_LENGTH)) > 0)
						writer.Write (data, 0, rlen);
				}
			} catch (Exception ex) {
				Log (ex, "Failed to write posted data to file '{4}'. Exception '{1}' was caught: '{2}'", fileName);
				InvalidRequest (context.Response, 500);
				return;
			}

			var state = new SenderState () {
				CsDataDir = csDataDir,
				CommitSourceID = commitSourceID
			};
			ThreadPool.QueueUserWorkItem (Sender, state);
		}

		void Sender (object state)
		{
			SenderState senderState = state as SenderState;
			if (String.IsNullOrEmpty (senderState.CsDataDir)) {
				Log (LogSeverity.Error, "Need a data directory to work.");
				return;
			}

			string filePath;
			senderState.Deserializer = new JsonDeserializer ();

			while (true) {
				filePath = GetNextWorkItem (senderState);
				if (String.IsNullOrEmpty (filePath))
					break;

				var sws = new SenderWorkerState () {
					State = senderState,
					WorkItemPath = filePath
				};

				ThreadPool.QueueUserWorkItem (SenderWorker, sws);
			}
		}

		void SenderWorker (object state)
		{
			SenderWorkerState sws = state as SenderWorkerState;
			if (sws == null) {
				Log (LogSeverity.Error, "Internal error - SenderWorker got a null state.");
				return;
			}

			string filePath = sws.WorkItemPath;
			SenderState senderState = sws.State;
			JsonDeserializer des = senderState.Deserializer;

			try {
				string fileText = HttpUtility.UrlDecode (File.ReadAllText (filePath));
				if (String.IsNullOrEmpty (fileText)) {
					Log (LogSeverity.Error, "Empty payload for item '{0}'", filePath);
					PutWorkItemBack (filePath, senderState);
					return;
				}

				if (!fileText.StartsWith ("payload=", StringComparison.Ordinal)) {
					Log (LogSeverity.Error, "Invalid payload format for item '{0}'", filePath);
					PutWorkItemBack (filePath, senderState);
					return;
				}

				fileText = fileText.Substring (8);
				Push push = des.Deserialize<Push> (fileText);
				push.CHAuthID = senderState.CommitSourceID;
				Mailer mailer = new Mailer ();
				if (mailer.Send (push)) {
					try {
						File.Delete (filePath);
					} catch (Exception ex) {
						Log (LogSeverity.Warning, "Failed to delete work item '{0}', the mail message might be sent twice. Exception {1} was thrown: {2}",
								filePath, ex.GetType (), ex.Message);
					}
				} else
					PutWorkItemBack (filePath, senderState);
			} catch (Exception ex) {
				Log (ex, "Attempt to send work item '{4}' failed. Exception {1} was thrown: {2}", Path.GetFileName (filePath));
				PutWorkItemBack (filePath, senderState);
			}
		}

		string GetNextWorkItem (SenderState senderState)
		{
			string csDataDir = senderState.CsDataDir;
			lock (senderDirectoryLock) {
				string file = null;

				try {
					string[] files = Directory.GetFiles (csDataDir, "*.json");
					if (files == null || files.Length == 0)
						return null;

					Array.Sort<string> (files);

					foreach (string f in files) {
						if (senderState.IsIgnored (f))
							continue;

						file = f;
						break;
					}
					if (file == null)
						return null;

					string newFile = file + ".processing";
					File.Move (file, newFile);

					return newFile;
				} catch (Exception ex) {
					if (!String.IsNullOrEmpty (file))
						PutWorkItemBack (file + ".processing", senderState);
					Log (ex, "Failed to retrieve next work item from directory '{4}'. Exception '{1}' was thrown: {2}", csDataDir);
					return null;
				}
			}
		}

		void PutWorkItemBack (string filePath, SenderState senderState)
		{
			if (String.IsNullOrEmpty (filePath))
				return;

			if (!filePath.EndsWith (".processing", StringComparison.Ordinal) || !File.Exists (filePath))
				return;

			string newFilePath = filePath.Substring (0, filePath.Length - 11);
			lock (senderDirectoryLock) {
				try {
					File.Move (filePath, newFilePath);
					Log (LogSeverity.Info, "Puting file '{0}' back in queue.", newFilePath);
					senderState.Ignore (newFilePath);
				} catch (Exception ex) {
					Log (ex, "Failed to put work item file '{4}' back in the queue. Exception '{1} was thrown: {2}", filePath);
					return;
				}
			}
		}

		void InvalidRequest (HttpResponse response, int statusCode)
		{
			response.ContentType = "text/html";
			response.StatusCode = statusCode;
			response.Clear ();

			response.Write ("Invalid request.");
			response.End ();
		}
	}
}

