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
using System.Xml;

using CaptainHook.GitHub;
using CaptainHook.Base;

namespace CaptainHook.Utils
{
	public class CommitSource : CommonBase
	{
		public string ID { get; private set; }
		public Author From { get; set; }
		public Author ReplyTo { get; set; }
		public bool SendAsCommitter { get; set; }
		public List <Author> TORecipients { get; private set; }
		public List <Author> CCRecipients { get; private set; }
		public List <Author> BCCRecipients { get; private set; }

		public CommitSource ()
		{
			TORecipients = new List <Author> ();
			CCRecipients = new List <Author> ();
			BCCRecipients = new List <Author> ();
			From = null;
			ID = null;
		}

		public void Read (XmlNode node)
		{
			if (node == null)
				return;
			
			XmlAttributeCollection attrs = node.Attributes;
			ID = attrs.GetRequired<string> ("id");
			SendAsCommitter = attrs.GetOptional ("sendAsCommitter", false);
			
			Author addr = ReadEmail (node.SelectSingleNode ("//from"));
			if (!SendAsCommitter && addr == null)
				throw new InvalidOperationException (String.Format ("Required <from> element is missing from commit source with id '{0}'", ID));
			From = addr;

			addr = ReadEmail (node.SelectSingleNode ("//replyTo"));
			if (addr == null)
				ReplyTo = From;
			else
				ReplyTo = addr;
			
			ReadEmails (node.SelectNodes ("//to/email"), TORecipients);
			if (TORecipients.Count == 0)
				throw new InvalidOperationException ("List of TO addresses must have at least one element");
			ReadEmails (node.SelectNodes ("//cc/email"), CCRecipients);
			ReadEmails (node.SelectNodes ("//bcc/email"), BCCRecipients);
		}

		Author ReadEmail (XmlNode node)
		{
			if (node == null)
				return null;

			string name, address;
			XmlAttributeCollection attrs = node.Attributes;
			name = attrs.GetOptional <string> ("name", null);
			address = attrs.GetOptional <string> ("address", null);

			if (address == null) {
				Log (LogSeverity.Warning, "Email address without the 'address' attribute in the commit source with id '{0}'", ID);
				return null;
			}
			
			return new Author () {
				Email = address,
				Name = name
			};
		}

		void ReadEmails (XmlNodeList nodes, List <Author> list)
		{
			Author addr;
			foreach (XmlNode email in nodes) {
				addr = ReadEmail (email);
				if (addr == null)
					continue;

				list.Add (addr);
			}
		}
	}
}
