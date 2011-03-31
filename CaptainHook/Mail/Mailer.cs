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
using System.Net.Mail;
using System.Threading;

using CaptainHook.Base;
using CaptainHook.GitHub;
using CaptainHook.Utils;

namespace CaptainHook.Mail
{
	public class Mailer : CommonBase
	{
		static readonly ReaderWriterLockSlim templateCacheLock = new ReaderWriterLockSlim ();
		static readonly Dictionary <string, ITemplate> templateCache = new Dictionary<string, ITemplate> (StringComparer.Ordinal);

		static Template<TData> GetMailTemplate<TData> (string commitSourceID)
		{
			if (String.IsNullOrEmpty (commitSourceID))
				throw new ArgumentNullException ("commitSourceID");

			bool readLocked = false, writeLocked = false;
			try {
				ITemplate ret = null;
				templateCacheLock.EnterUpgradeableReadLock ();
				readLocked = true;

				if (templateCache.TryGetValue (commitSourceID, out ret))
					return (Template<TData>)ret;

				templateCacheLock.EnterWriteLock ();
				writeLocked = true;

				ret = new Template<TData> (Config.Instance.FullBaseTemplatePath, commitSourceID);
				ret.TemplateName = "mail";
				ret.Compile ();

				templateCache.Add (commitSourceID, ret);
				return (Template <TData>)ret;
			} finally {
				if (writeLocked)
					templateCacheLock.ExitWriteLock ();
				if (readLocked)
					templateCacheLock.ExitUpgradeableReadLock ();
			}
		}

		public bool Send (Push item)
		{
			if (item == null)
				throw new ArgumentNullException ("item");

			List<Commit> commits = item.Commits;
			if (commits == null || commits.Count == 0) {
				Log (LogSeverity.Info, "Push has no commits. Not sending mails.");
				return false;
			}
			SmtpServerConfig smtpConfig = Config.Instance.SmtpServer;
			MailMessage message = null;

			try {
				foreach (Commit c in commits)
					if (!c.FetchDiff (item))
						return false;

				var template = GetMailTemplate<Push> (item.CHAuthID);
				message = template.ComposeMail (item);
				if (message == null) {
					Log (LogSeverity.Error, "No mail message composed.");
					return false;
				}

				var smtp = new SmtpClient ();
				smtp.Host = smtpConfig.Host;
				smtp.Port = smtpConfig.Port;
				smtp.EnableSsl = smtpConfig.EnableSSL;
				smtp.Credentials = smtpConfig.Credentials;
				smtp.Send (message);
			} catch (SmtpException ex) {
				Log (ex, "While sending mail. SMTP failure code: {4}.\n{0}", ex.StatusCode);
				return false;
			} catch (Exception ex) {
				Log (ex, "While sending mail.\n{0}");
				return false;
			} finally {
				if (message != null)
					message.Dispose ();
			}
			
			return true;
		}
	}
}
