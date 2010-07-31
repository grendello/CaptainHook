// 
//  Authors:
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
//     * Neither the name of Novell, Inc nor names of the contributors may be used to endorse or promote products derived from this software without specific prior written permission.
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
using System.Net;

using CaptainHook.Base;
using CaptainHook.Utils;

namespace CaptainHook.GitHub
{
	public class Commit : CommonBase
	{
		// GitHub API members
		public List <string> Added { get; set; }
		public Author Author { get; set; }
		public string ID { get; set; }
		public string Message { get; set; }
		public List <string> Modified { get; set; }
		public List <string> Removed { get; set; }
		public DateTime TimeStamp { get; set; }
		public string Url { get; set; }

		// Our members
		public CommitWithDiff Diff { get; private set; }
		
		public bool FetchDiff (Push push)
		{
			WebClient client = new WebClient ();
			string url = String.Format ("http://github.com/api/v2/json/commits/show/{0}/{1}/{2}",
						    push.Repository.Owner.Name,
						    push.Repository.Name,
						    ID);

			string response;
			try {
				response = client.DownloadString (url);
				var jdes = new JsonDeserializer ();
				var wrapper = jdes.Deserialize<CommitWithDiffJsonWrapper> (response);
				if (wrapper != null) {
					var diff = wrapper.Commit;
					if (!diff.FetchBlobs (push)) {
						Log (LogSeverity.Error, "Failed to fetch blobs for commit '{0}'", ID);
						return false;
					}
					Diff = diff;
				} else {
					Log (LogSeverity.Error, "Failed to fetch diff for commit '{0}'", ID);
					return false;
				}
			} catch (Exception ex) {
				Log (ex);
				return false;
			}
			
			return Diff != null;
		}
	}
}
