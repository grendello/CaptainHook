// 
//  Author:
//    Marek Habersack grendel@twistedcode.net
// 
//  Copyright (c) 2011, Marek Habersack
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

namespace CaptainHook.Web.Processing
{
	class CommitSender : CommonBase
	{
		static readonly object senderDirectoryLock = new object ();
		
		public void Send (SenderState state)
		{
			ThreadPool.QueueUserWorkItem (Sender, state);
		}
		
		void Sender (object state)
		{
			SenderState senderState = state as SenderState;
			if (String.IsNullOrEmpty (senderState.CsDataDir)) {
				Log (LogSeverity.Error, "Need a data directory to work.");
				return;
			}
			
			if (!Directory.Exists (senderState.CsDataDir))
				return;
			
			string filePath;
			senderState.Deserializer = new JsonDeserializer ();

			while (true) {
				filePath = GetNextWorkItem (senderState);
				if (filePath == null)
					break;

				var sws = new SenderWorkerState () {
					State = senderState,
					WorkItemPath = filePath
				};
				Thread th = new Thread (_ => {
					SenderWorker (sws); });
				th.IsBackground = true;
				th.Start ();
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
					
					try {
						CachingFetcher.RemoveCommitsFromCache (push.CHAuthID, push.Commits);
					} catch (Exception ex) {
						Log (ex, "Failed to clean up commit cache, some files may have been left behind. {2} ({1})");
					}
				} else {
					Log (LogSeverity.Info, "Mail not sent.");
					PutWorkItemBack (filePath, senderState, (push.Commits == null || push.Commits.Count == 0));
				}
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
			PutWorkItemBack (filePath, senderState, false);
		}
		
		void PutWorkItemBack (string filePath, SenderState senderState, bool isEmpty)
		{
			if (String.IsNullOrEmpty (filePath))
				return;

			if (!filePath.EndsWith (".processing", StringComparison.Ordinal) || !File.Exists (filePath))
				return;

			string newFilePath;
			
			if (isEmpty)
				newFilePath = filePath.Replace (".processing", ".empty");
			else
				newFilePath = filePath.Substring (0, filePath.Length - 11);
			lock (senderDirectoryLock) {
				try {
					File.Move (filePath, newFilePath);
					if (isEmpty)
						Log (LogSeverity.Info, "Push without commits, file '{0}' marked as empty (will not be processed again).", newFilePath);
					else {
						Log (LogSeverity.Info, "Puting file '{0}' back in queue.", newFilePath);
						senderState.Ignore (newFilePath);
					}
				} catch (Exception ex) {
					Log (ex, "Failed to put work item file '{4}' back in the queue. Exception '{1} was thrown: {2}", filePath);
					return;
				}
			}
		}
	}
}

