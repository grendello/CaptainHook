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
using System.Collections.Generic;
using System.IO;
using System.Threading;

using CaptainHook.Base;
using CaptainHook.Utils;

namespace CaptainHook.Web.Processing
{
	public class PeriodicSender : CommonBase
	{
		sealed class CommitSourceLocation
		{
			public CommitSource Source;
			public string DataPath;
		}
		
		CommitSender commitSender;
		int interval = 0;

		public void Init ()
		{
			uint interval = Config.Instance.PeriodicSenderInterval;
			if (interval == 0) {
				Log (LogSeverity.Info, "No periodic queue processing configured.");
				return;
			}
			
			this.interval = (int)interval * 1000;
			commitSender = new CommitSender ();
			Thread checker = new Thread (Checker);
			checker.IsBackground = true;
			checker.Start ();
			Log (LogSeverity.Info, "Periodic sender started with interval of {0}s", interval);			
		}

		void Checker (object state)
		{
			var locations = new List <CommitSourceLocation> ();
			Config cfg = Config.Instance;
			string root = cfg.RootPath;
			foreach (var kvp in cfg.CommitSources) {
				locations.Add (new CommitSourceLocation () {
					Source = kvp.Value,
					DataPath = Path.Combine (Path.Combine (root, "data"), kvp.Value.ID)
				});
			}
			
			while (true) {
				try {
					CheckerLoop (locations);
				} catch (Exception ex) {
					Log (ex, "Exception in periodic checker loop.\n{0}");
				}
			}
		}
		
		void CheckerLoop (List <CommitSourceLocation> locations)
		{
			while (true) {
				Log (LogSeverity.Debug, "Periodic backlog processing starting at {0}", DateTime.Now);
				foreach (CommitSourceLocation location in locations) {
					try {
						commitSender.Send (new SenderState {
							CsDataDir = location.DataPath,
							CommitSourceID = location.Source.ID
						});
					} catch (Exception ex) {
						Log (ex, "Exception while periodic processing of commit source '{4}' in directory '{5}'.\n{0}",
						location.Source.ID, location.DataPath);
					}
				}
				Log (LogSeverity.Debug, "Periodic backlog processing finished at {0}", DateTime.Now);
				Thread.Sleep (interval);
			}
		}
	}
}

