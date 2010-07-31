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
using System.Net;
using System.Security;
using System.Xml;

namespace CaptainHook.Utils
{
	public class SmtpServerConfig
	{
		string user;

		// Mono implementation is not as secure as .NET's - the data is not encrypted at the moment :(
		// Also, since Mono's NetworkCredential doesn't implement the constructor overloads which take
		// SecureString, we use plain string for now
		// SecureString password;
		string password;
		NetworkCredential credentials;

		public string Host { get; private set; }
		public int Port { get; private set; }
		public bool EnableSSL { get; private set; }
		public NetworkCredential Credentials {
			get {
				if (String.IsNullOrEmpty (user) || String.IsNullOrEmpty (password))
					return null;

				if (credentials == null)
					credentials = new NetworkCredential (user, password);

				return credentials;
			}
		}

		public SmtpServerConfig ()
		{
			Host = "127.0.0.1";
			Port = 25;
			EnableSSL = false;
		}

		public void Read (XmlNode node)
		{
			if (node == null)
				throw new ArgumentNullException ("node");

			XmlAttributeCollection attrs = node.Attributes;
			string host = attrs.GetRequired<string> ("host");
			if (host.Length == 0)
				throw new InvalidOperationException ("The host attribute must not be empty.");
			Host = host;
			int port = attrs.GetOptional<int> ("port", 25);
			if (port < 1 || port > 65535)
				throw new InvalidOperationException ("Port must be an integer between 1 and 65535");
			Port = port;
			EnableSSL = attrs.GetOptional<bool> ("enableSSL", true);
			user = attrs.GetOptional<string> ("user", null);
			password = attrs.GetOptional<string> ("password", null);
		}
	}
}

