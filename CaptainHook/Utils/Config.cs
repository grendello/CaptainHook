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
using System.IO;
using System.Reflection;
using System.Security;
using System.Xml;

using CaptainHook.Base;

namespace CaptainHook.Utils
{
	public class Config : CommonBase
	{
		const string DEFAULT_BASE_TEMPLATE_PATH = "templates";
		
		static Config instance;
		static readonly string chVersionString;
		
		string directorySeparatorString;
		Dictionary <string, CommitSource> commitSources;
		string fullBaseTemplatePath;
		string rootPath;
		string baseTemplatePath;
		string gitHubLogin;
		string gitHubApiToken;
		
		public Dictionary <string, CommitSource> CommitSources {
			get {
				if (commitSources == null)
					commitSources = new Dictionary <string, CommitSource> (StringComparer.Ordinal);

				return commitSources;
			}
		}
		
		public string GitHubLogin { 
			get {
				if (String.IsNullOrEmpty (gitHubLogin)) {
					Log (LogSeverity.Error, "Missing GitHub Login setting in configuration. Requests to GitHub will fail.");
					return String.Empty;
				}
				return gitHubLogin;
			}

			set { gitHubLogin = value; }
		}
		public string GitHubApiToken { 
			get {
				if (String.IsNullOrEmpty (gitHubApiToken)) {
					Log (LogSeverity.Error, "Missing GitHub API Token setting in configuration. Requests to GitHub will fail.");
					return String.Empty;
				}
				return gitHubApiToken;
			}

			set { gitHubApiToken = value; }
		}
		
		public string Version { 
			get { return chVersionString; }
		}
		public bool Debug { get; set; }
		public SmtpServerConfig SmtpServer { get; set; }
		public uint PeriodicSenderInterval { get; set; }
		
		public string RootPath {
			get { return rootPath; }
			
			set {
				rootPath = value;
				fullBaseTemplatePath = null;
			}
		}
		
		public string BaseTemplatePath {
			get { return baseTemplatePath; }
			
			set {
				baseTemplatePath = value;
				fullBaseTemplatePath = null;
			}
		}
		
		public string FullBaseTemplatePath {
			get {
				if (String.IsNullOrEmpty (fullBaseTemplatePath)) {
					string basePath = BaseTemplatePath;
					if (String.IsNullOrEmpty (basePath))
						basePath = DEFAULT_BASE_TEMPLATE_PATH;
					else if (directorySeparatorString != null)
						basePath = basePath.Replace ("/", directorySeparatorString);
					
					if (basePath [0] != '/') {
						string rootPath = RootPath;
						if (String.IsNullOrEmpty (rootPath))
							rootPath = Directory.GetCurrentDirectory ();
						
						fullBaseTemplatePath = Path.Combine (RootPath, basePath);
					} else
						fullBaseTemplatePath = basePath;
				}

				return fullBaseTemplatePath;
			}
			
		}
		
		public static Config Instance {
			get { return instance; }
		}
		
		static Config ()
		{
			instance = new Config ();
			Assembly asm = typeof (Config).Assembly;
			Version version = asm.GetName ().Version;
			if (version != null)
				chVersionString = String.Format ("CaptainHook {0}.{1}", version.Major, version.Minor);
			else
				chVersionString = "CaptainHook X.Y";
		}
		
		Config ()
		{
			RootPath = Directory.GetCurrentDirectory ();
			BaseTemplatePath = DEFAULT_BASE_TEMPLATE_PATH;
			PeriodicSenderInterval = 300;

			char ch = Path.DirectorySeparatorChar;
			if (ch != '/')
				directorySeparatorString = ch.ToString ();
		}

		public bool IsKnownCommitSource (string id)
		{
			if (String.IsNullOrEmpty (id))
				return false;

			return CommitSources.ContainsKey (id);
		}

		public void LoadConfigFile (string path)
		{
			if (String.IsNullOrEmpty (path))
				throw new ArgumentNullException ("path");

			if (!File.Exists (path))
				throw new InvalidOperationException ("Configuration file not found.");

			XmlDocument doc;
			try {
				doc = new XmlDocument ();
				doc.Load (path);
			} catch (Exception ex) {
				Log (ex, "Exception while loading configuration from {4}.\n{0}", path);
				throw;
			}

			ReadOptions (doc.SelectSingleNode ("//CaptainHook/options"));
			ReadCommitSources (doc.SelectNodes ("//CaptainHook/commitSource"));
		}

		void ReadGitHubApiSettings (XmlNode credentials)
		{
			if (credentials == null) {
				Log (LogSeverity.Error, "Required configuration element <options><gitHubApiCredentials/></options> is missing.");
				return;
			}
			
			GitHubLogin = credentials.Attributes.GetRequired <string> ("login");
			GitHubApiToken = credentials.Attributes.GetRequired <string> ("token");
		}

		void ReadCommitSources (XmlNodeList sources)
		{
			if (sources == null)
				return;

			Dictionary <string, CommitSource> commitSources = CommitSources;
			foreach (XmlNode node in sources) {
				var cs = new CommitSource ();
				try {
					cs.Read (node);
					commitSources.Add (cs.ID, cs);
				} catch (Exception ex) {
					Log (ex, "Failure to read commit source with ID '{4}'.\n{0}", cs.ID);
				}
			}
		}
		
		void ReadOptions (XmlNode options)
		{
			if (options == null)
				return;

			XmlNode node = options.SelectSingleNode ("//debug");
			if (node != null)
				Debug = true;

			node = options.SelectSingleNode ("//baseTemplatePath");
			if (node != null)
				BaseTemplatePath = node.Attributes.GetRequired<string> ("value");

			node = options.SelectSingleNode ("//smtpServer");
			var smtpCfg = new SmtpServerConfig ();
			if (node != null) {
				try {
					smtpCfg.Read (node);
				} catch (Exception ex) {
					Log (ex, "Failure to read SMTP server configuration from the config file. Falling back to default settings.");
					smtpCfg = new SmtpServerConfig ();
				}
			}
			SmtpServer = smtpCfg;
			
			node = options.SelectSingleNode ("//periodicSender");
			if (node != null)
				PeriodicSenderInterval = node.Attributes.GetRequired <uint> ("interval");
			
			ReadGitHubApiSettings (node.SelectSingleNode ("//gitHubApiCredentials"));
		}
	}
}
