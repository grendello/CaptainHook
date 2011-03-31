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
using System.Text;
using System.Web;

using CaptainHook.Base;
using CaptainHook.Utils;

namespace CaptainHook.GitHub
{
	public class CommitWithDiff : CommonBase
	{
		// GitHub API members
		public List <string> Added { get; set; }
		public Author Author { get; set; }
		public Author Committer { get; set; }
		public string ID { get; set; }
		public string Message { get; set; }
		public List <CommitModifiedFile> Modified { get; set; }
		public List <string> Removed { get; set; }
		public string Url { get; set; }
		public DateTime Committed_Date { get; set; }
		public DateTime Authored_Date { get; set; }
		public string Tree { get; set; }

		// Our members
		public List<Blob> AddedBlobs {
			get; private set;
		}

		public string GetFullDiff ()
		{
			var sb = new StringBuilder ();
			List<CommitModifiedFile> modified = Modified;
			if (modified != null && modified.Count > 0) {
				sb.AppendLine ();
				foreach (CommitModifiedFile file in modified) {
					sb.AppendFormat ("Modified: {0}", file.Filename);
					sb.AppendLine ();
					sb.Append ("===================================================================");
					sb.AppendLine ();
					sb.Append (file.Diff);
					sb.AppendLine ();
				}
			}

			List<Blob> addedBlobs = AddedBlobs;
			if (addedBlobs != null && addedBlobs.Count > 0) {
				sb.AppendLine ();
				foreach (Blob blob in addedBlobs) {
					sb.AppendFormat ("Added: {0}", blob.Name);
					sb.AppendLine ();
					sb.Append ("===================================================================");
					sb.AppendLine ();
					sb.Append (blob.Diff);
					sb.AppendLine ();
				}
			}

			return sb.ToString ();
		}

		public bool FetchBlobs (Push push)
		{
			if (push == null)
				throw new ArgumentNullException ("push");

			List<string> added = Added;
			if (added == null || added.Count == 0)
				return true;

			Blob blob;
			var list = new List<Blob> ();
			string ownerName = push.Repository.Owner.Name;
			string repositoryName = push.Repository.Name;
			string tree = Tree;
			string commitSource = push.CHAuthID;
			
			foreach (string file in added) {
				blob = Fetch (commitSource, repositoryName, ownerName, tree, file);
				if (blob == null)
					return false;
				list.Add (blob);
			}

			if (list.Count > 0)
				AddedBlobs = list;
			else
				AddedBlobs = null;

			return true;
		}

		Blob Fetch (string commitSource, string repositoryName, string repositoryOwner, string tree, string file)
		{
			string response;
			string url = null;
			try {
				response = CachingFetcher.FetchBlob (commitSource, repositoryOwner, repositoryName, tree, file, out url);
				var jdes = new JsonDeserializer ();
				var wrapper = jdes.Deserialize<BlobJsonWrapper> (response);
				if (wrapper != null) {
					var blob = wrapper.Blob;
					if (blob == null)
						Log (LogSeverity.Error, "Empty blob for commit '{0}', file '{1}'", ID, file);
					return blob;
				} else {
					Log (LogSeverity.Error, "Failed to fetch blob for commit '{0}', file '{1}'", ID, file);
					return null;
				}
			} catch (WebException ex) {
				var resp = ex.Response as HttpWebResponse;
				if (resp != null && resp.StatusCode == HttpStatusCode.NotFound)
					Log (ex, "A HTTP NotFound error while fetching blob for commit '{4}'. Failed URL was '{5}'.\n{0}",
						ID, url);
				else
					Log (ex);
				return null;
			} catch (Exception ex) {
				Log (ex);
				return null;
			}
		}
	}
}
