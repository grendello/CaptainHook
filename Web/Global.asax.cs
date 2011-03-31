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
using System.IO;
using System.Web;
using System.Web.Hosting;
using System.Web.Configuration;

using CaptainHook.Utils;
using CaptainHook.Web.Processing;

namespace CaptainHook.Web
{
	public class Global : HttpApplication
	{
		PeriodicSender periodicSender;

		protected virtual void Application_Start (object sender, EventArgs e)
		{
			Config config = Config.Instance;
			Console.Error.WriteLine (config.Version + " starting");
			string configDir = WebConfigurationManager.AppSettings ["ConfigDirectory"];
			if (String.IsNullOrEmpty (configDir))
				throw new InvalidOperationException ("ConfigDirectory application setting must be present in web.config");
			configDir = HostingEnvironment.MapPath (configDir);

			if (!Directory.Exists (configDir))
				throw new InvalidOperationException ("Config directory must exist.");

			string configFile = Path.Combine (configDir, "CaptainHook.xml");
			if (!File.Exists (configFile))
				throw new InvalidOperationException ("Config file must exist.");

			config.RootPath = configDir;
			config.LoadConfigFile (configFile);

			string csDataDir = Path.Combine (Config.Instance.RootPath, "data");
			if (Directory.Exists (csDataDir)) {
				try {
					string[] files = Directory.GetFiles (csDataDir, "*.json.processing", SearchOption.AllDirectories);
					if (files != null && files.Length > 0) {
						string newFile;
						foreach (string f in files) {
							newFile = f.Substring (0, f.Length - 11);
							try {
								File.Move (f, newFile);
							} catch (Exception ex) {
								Console.Error.WriteLine ("Error moving {0} to {1}", f, newFile);
								Console.Error.WriteLine (ex);
							}
						}
					}
				} catch (Exception ex) {
					Console.Error.WriteLine ("Error processing stale files on application startup:");
					Console.Error.WriteLine (ex);
				}
			}
			CachingFetcher.Init ();
			periodicSender = new PeriodicSender ();
			periodicSender.Init ();
		}
	}
}