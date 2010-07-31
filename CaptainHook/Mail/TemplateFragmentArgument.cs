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
	public class TemplateFragmentArgument : TemplateFragment
	{
		List <TemplateFragment> fragments;

		public List <TemplateFragment> Fragments {
			get {
				if (fragments == null)
					fragments = new List <TemplateFragment> ();

				return fragments;
			}
		}

		public TemplateFragmentMacro Parent {
			get; set;
		}

		public TemplateFragmentArgument ()
		{}

		public TemplateFragmentArgument (string inputFile, TemplateFragmentMacro parent)
		{
			if (String.IsNullOrEmpty (inputFile))
				throw new ArgumentNullException ("inputFile");

			if (parent == null)
				throw new ArgumentNullException ("parent");
			
			this.InFile = inputFile;
		}

		public override void Append (char ch)
		{
			List <TemplateFragment> fragments = Fragments;
			int count = fragments.Count;
			TemplateFragment fragment = null;
			
			if (count != 0)
				fragment = fragments [count - 1];

			if (!(fragment is TemplateFragmentPlainText)) {
				fragment = new TemplateFragmentPlainText (InFile);
				fragments.Add (fragment);
			}
			fragment.Append (ch);
		}

		public override string ToString ()
		{
			var sb = new StringBuilder ();

			if (fragments == null || fragments.Count == 0)
				return String.Empty;

			for (int i = 0; i < fragments.Count; i++) {
				sb.Append (fragments [i].ToString ());
				if (i + 1 < fragments.Count)
					sb.Append (", ");
			}

			return sb.ToString ();
		}

		public override void Validate ()
		{
			base.Validate ();
			if (Parent == null)
				throw new InvalidOperationException ("Each TemplateFragmentArgument must have a parent.");
		}
	}
}
