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
using System.Text;

namespace CaptainHook.Mail
{
	public class TemplateElementMailHeader : TemplateElement
	{
		List<TemplateElementArgument> arguments;

		protected List<TemplateElementArgument> Arguments {
			get { return arguments; }
		}
		
		public string Name { get; set; }

		public TemplateElementMailHeader (List<TemplateElementArgument> arguments)
		{
			this.arguments = arguments;
		}

		public TemplateElementMailHeader (string name, List<TemplateElementArgument> arguments)
			: this (arguments)
		{
			if (String.IsNullOrEmpty (name))
				throw new ArgumentNullException ("name");

			this.Name = name;
		}

		public override string Generate (object data)
		{
			if (data == null)
				throw new ArgumentNullException ("data");

			if (arguments == null || arguments.Count == 0)
				return String.Empty;

			var sb = new StringBuilder ();
			foreach (TemplateElementArgument arg in arguments)
				sb.Append (arg.Generate (data));

			string ret = sb.ToString ();
			int eq = ret.IndexOf ('=');
			if (eq == -1)
				return ret;

			if (String.IsNullOrEmpty (Name))
				Name = ret.Substring (0, eq);

			return ret.Substring (eq + 1);
		}
	}
}

