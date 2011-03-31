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
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Web;

using CaptainHook.Base;
using CaptainHook.GitHub;

namespace CaptainHook.Utils
{
	public class CachingFetcher : CommonBase
	{
		const string gitHubCommitUrlFormat = "http://github.com/api/v2/json/commits/show/{0}/{1}/{2}?login={3}&token={4}";
		const string gitHubBlobUrlFormat = "http://github.com/api/v2/json/blob/show/{0}/{1}/{2}/{3}?login={4}&token={5}";
		
		static bool initialized;
		static string cacheRootPath;
		
		public static void Init ()
		{
			if (initialized)
				return;
			
			cacheRootPath = Path.Combine (Config.Instance.RootPath, "cache");
			if (CreateDir (cacheRootPath))
				Log (LogSeverity.Debug, "Initialized commit/blob cache directory '{0}'", cacheRootPath);
			
			initialized = true;
		}
		
		static string GetCachedFilePath (string commitSource, string commitId, string extension, bool createIfCacheDirMissing)
		{
			string commitSourceCachePath = Path.Combine (cacheRootPath, commitSource);
			bool canCache;
			if (createIfCacheDirMissing) {
				canCache = CreateDir (commitSourceCachePath);
			} else
				canCache = Directory.Exists (commitSourceCachePath);
			
			return canCache ? Path.Combine (commitSourceCachePath, commitId + extension) : null;
		}
		
		static string Fetch (string commitSource, string ownerName, string repositoryName, string id, string cachedFilePath, string url)
		{
			if (!initialized) {
				Log (LogSeverity.Error, "CachingFetcher not initialized.");
				return null;
			}
			
			string response;
			
			if (cachedFilePath != null && File.Exists (cachedFilePath)) {
				try {
					response = File.ReadAllText (cachedFilePath, Encoding.UTF8);
					Log (LogSeverity.Debug, "Payload with ID '{0}' was cached on disk, not fetching from GitHub.", id);
					return response;
				} catch (Exception ex) {
					Log (ex, "Failed to read cached payload from '{4}'. Will re-fetch from GitHub.\n{0}", cachedFilePath);
				}
			}
			
			try {
				Log(LogSeverity.Debug, "Fetch: {0}", url);
				using (WebClient client = new CHWebClient ()) {
					response = client.DownloadString (url);
					Throttle (client);
				}
				
				if (cachedFilePath != null) {
					try {
						File.WriteAllText (cachedFilePath, response, Encoding.UTF8);
						Log (LogSeverity.Debug, "Payload with ID '{0}' cached in file '{1}'.", id, cachedFilePath);
					} catch (Exception ex) {
						Log (ex, "Failed to write payload with id '{4}' to cache file '{5}'. Further requests will re-fetch it from GitHub.\n{0}", id, cachedFilePath);
					}
				}
				
				return response;
			} catch (Exception ex) {
				Log (ex, "Exception while fetching diff for commit '{4}' from URL '{5}'\n{0}", id, url);
			}
			
			return null;
		}
		
		public static string FetchBlob (string commitSource, string ownerName, string repositoryName, string treeId, string file, out string url)
		{
			file = HttpUtility.UrlEncode (file);
			string cachedFilePath = GetCachedFilePath (commitSource, treeId, file + ".blob", true);
			Config config = Config.Instance;
			url = String.Format (gitHubBlobUrlFormat, ownerName, repositoryName, treeId, file, config.GitHubLogin, config.GitHubApiToken);
			return Fetch (commitSource, ownerName, repositoryName, treeId, cachedFilePath, url);
		}
		
		public static string FetchDiff (string commitSource, string ownerName, string repositoryName, string commitId, out string url)
		{
			string cachedFilePath = GetCachedFilePath (commitSource, commitId, ".commit", true);
			Config config = Config.Instance;
			url = String.Format (gitHubCommitUrlFormat, ownerName, repositoryName, commitId, config.GitHubLogin, config.GitHubApiToken);
			return Fetch (commitSource, ownerName, repositoryName, commitId, cachedFilePath, url);
		}
		
		public static void RemoveCommitsFromCache (string commitSource, List <Commit> commits)
		{
			if (commits == null || commits.Count == 0 || String.IsNullOrEmpty (commitSource))
				return;
			
			foreach (Commit commit in commits) {
				if (commit == null || String.IsNullOrEmpty (commit.ID))
					continue;
				
				CommitWithDiff cwd = commit.Diff;
				if (cwd != null) {
					try {
						RemoveBlobsFromCache (commitSource, cwd.Tree, cwd.AddedBlobs);
					} catch (Exception ex) {
						Log (ex);
					}
				}
				string cachedCommitPath = GetCachedFilePath (commitSource, commit.ID, ".commit", false);
				if (!File.Exists (cachedCommitPath))
					continue;
				try {
					File.Delete (cachedCommitPath);
					Log (LogSeverity.Debug, "Removed cached commit file '{0}'", cachedCommitPath);
				} catch (Exception ex) {
					Log (ex, "Failed to remove commit file '{4}' from disk cache. Please do it manually.", cachedCommitPath);
				}
			}
		}
		
		static void RemoveBlobsFromCache (string commitSource, string treeId, List <Blob> blobs)
		{
			if (blobs == null || blobs.Count == 0 || String.IsNullOrEmpty (commitSource))
				return;
			
			foreach (Blob blob in blobs) {
				if (blob == null || String.IsNullOrEmpty (blob.Name))
					continue;
				
				string cachedBlobPath = GetCachedFilePath (commitSource, treeId, HttpUtility.UrlEncode (blob.Name) + ".blob", false);
				if (!File.Exists (cachedBlobPath))
					continue;
				try {
					File.Delete (cachedBlobPath);
					Log (LogSeverity.Debug, "Removed cached blob file '{0}'", cachedBlobPath);
				} catch (Exception ex) {
					Log (ex, "Failed to remove blob file '{4}' from disk cache. Please do it manually.", cachedBlobPath);
				}
			}
		}
		
		static bool CreateDir (string path)
		{
			if (Directory.Exists (path))
				return true;
			
			try {
				Directory.CreateDirectory (path);
			} catch (Exception ex) {
				Log (ex);
				return false;
			}
			
			return true;
		}
	}
}

