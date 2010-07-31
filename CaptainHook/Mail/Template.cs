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
using System.Net.Mail;
using System.Text;

using CaptainHook.Base;
using CaptainHook.GitHub;
using CaptainHook.Utils;

namespace CaptainHook.Mail
{
	public class Template <TData> : CommonBase, ITemplate
	{
		string templateName;
		string basePath;
		string commitSourceID;
		string fullTemplatePath;
		string fullPrefixedTemplatePath;
		
		List <TemplateElement> compiled;
		TemplateElementFactory <TData> factory;

		public string TemplateName {
			get {
				if (String.IsNullOrEmpty (templateName))
					templateName = typeof (TData).Name.ToLower ();

				return templateName;
			}

			set {
				templateName = value;
				fullTemplatePath = null;
			}
		}

		public string FullTemplatePath {
			get {
				if (String.IsNullOrEmpty (fullTemplatePath))
					fullTemplatePath = Path.Combine (basePath, TemplateName + ".txt");

				return fullTemplatePath;
			}
		}

		public string FullPrefixedTemplatePath {
			get {
				if (String.IsNullOrEmpty (fullPrefixedTemplatePath))
					fullPrefixedTemplatePath = Path.Combine (basePath, commitSourceID + "." + TemplateName + ".txt");

				return fullPrefixedTemplatePath;
			}
		}
		
		public Template (string basePath, string commitSourceID)
		{
			if (String.IsNullOrEmpty (basePath))
				throw new ArgumentNullException ("basePath");

			if (String.IsNullOrEmpty (commitSourceID))
				throw new ArgumentNullException ("commitSourceID");

			this.basePath = basePath;
			this.commitSourceID = commitSourceID;
		}

		public bool Compile ()
		{
			if (compiled != null)
				return true;

			string path = FullPrefixedTemplatePath;
			if (!File.Exists (path)) {
				path = FullTemplatePath;
				if (!File.Exists (path))
					throw new InvalidOperationException (String.Format ("Template '{0}' does not exist in directory '{1}'.", TemplateName, basePath));
			}

			compiled = new List <TemplateElement> ();
			factory = new TemplateElementFactory <TData> (basePath, commitSourceID);
			try {
				var parser = new TemplateParser (path);
				parser.FragmentParsed += new EventHandler<FragmentParsedEventArguments> (OnFragmentParsed);
				parser.Parse ();
			} catch (Exception ex) {
				Log (ex);
				return false;
			} finally {
				factory = null;
			}

			return compiled != null;
		}

		void OnFragmentParsed (object sender, FragmentParsedEventArguments args)
		{
			compiled.Add (factory.GetElement (args.Fragment));
		}

		public MailMessage ComposeMail (object item)
		{
			if (item == null)
				throw new ArgumentNullException ("item");
			if (!(item is TData))
				throw new ArgumentException (String.Format ("Must be an instance of the '{0}' type", typeof(TData).FullName), "item");

			return ComposeMail ((TData)item);
		}

		public MailMessage ComposeMail (TData item)
		{
			if (item == null)
				throw new ArgumentNullException ("item");

			if (!Compile ())
				return null;

			var ret = new MailMessage ();
			ret.Body = ComposeMailBody (item, ret);

			CommitSource cs;
			if (!Config.Instance.CommitSources.TryGetValue (commitSourceID, out cs) || cs == null)
				throw new InvalidOperationException (String.Format ("Missing configuration for commit source with ID '{0}'", commitSourceID));

			AddAddresses (ret.CC, cs.CCRecipients);
			AddAddresses (ret.Bcc, cs.BCCRecipients);
			AddAddresses (ret.To, cs.TORecipients);

			Author author = cs.From;
			ret.From = new MailAddress (author.Email, author.Name);

			author = cs.ReplyTo;
			ret.ReplyTo = new MailAddress (author.Email, author.Name);

			return ret;
		}

		void AddAddresses (MailAddressCollection coll, List<Author> list)
		{
			if (coll == null || list == null || list.Count == 0)
				return;

			string name;
			MailAddress address;
			foreach (var author in list) {
				name = author.Name;
				if (String.IsNullOrEmpty (name))
					address = new MailAddress (author.Email);
				else
					address = new MailAddress (author.Email, name);
				coll.Add (address);
			}
		}

		public string ComposeMailBody (object item)
		{
			return ComposeMailBody ((TData)item);
		}

		public string ComposeMailBody (TData item)
		{
			if (item == null)
				throw new ArgumentNullException ("item");

			if (!Compile ())
				return null;

			return ComposeMailBody (item, null);
		}

		string ComposeMailBody (TData item, MailMessage message)
		{
			var sb = new StringBuilder ();
			string data;
			bool skipNewlineIfEmpty = false;
			bool dataEmpty;

			foreach (TemplateElement element in compiled) {
				if (element is TemplateElementMailHeader) {
					if (message != null) {
						SetMessageHeader (element as TemplateElementMailHeader, message, item);
						skipNewlineIfEmpty = true;
					}
					continue;
				}

				data = element.Generate (item);
				dataEmpty = String.IsNullOrEmpty (data);
				if (skipNewlineIfEmpty && !dataEmpty && element is TemplateElementText) {
					int newline = data.IndexOf ('\n');
					bool skip = true;
					if (newline == -1)
						skip = false;
					else {
						for (int i = 0; i < newline; i++) {
							if (!Char.IsWhiteSpace (data[i])) {
								skip = false;
								break;
							}
						}
					}

					if (skip) {
						if (newline < data.Length - 1)
							data = data.Substring (newline + 1);
						else
							continue;
						dataEmpty = String.IsNullOrEmpty (data);
					}
				}

				if (dataEmpty)
					skipNewlineIfEmpty = element.SkipNewlineIfLineEmpty;
				else {
					skipNewlineIfEmpty = false;
					sb.Append (data);
				}
			}

			return sb.ToString ();
		}

		void SetMessageHeader (TemplateElementMailHeader element, MailMessage message, TData item)
		{
			string val = element.Generate (item);
			if (String.Compare ("subject", element.Name, StringComparison.OrdinalIgnoreCase) == 0) {
				message.Subject = val;
				return;
			}

			message.Headers.Add (element.Name, val);
		}
	}
}
