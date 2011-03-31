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
using System.Collections;
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
using CaptainHook.Web.Processing;

namespace CaptainHook.Web.Handlers
{
	sealed class GitHubJsonPostHandler : CommonBase, IHttpHandler
	{
		const int INPUT_BUFFER_LENGTH = 4096;
		CommitSender commitSender;
		
		public bool IsReusable {
			get { return true; }
		}
		
		public GitHubJsonPostHandler ()
		{
			this.commitSender = new CommitSender ();
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
				return;
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

			commitSender.Send (new SenderState () {
				CsDataDir = csDataDir,
				CommitSourceID = commitSourceID
			});
		}

		static void InvalidRequest (HttpResponse response, int statusCode)
		{
			response.Clear ();
			response.ContentType = "text/plain";
			response.StatusCode = statusCode;

			response.Write ("Invalid request.");
		}
	}
}

