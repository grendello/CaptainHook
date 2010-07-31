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
using System.Text;

using CaptainHook.Base;

namespace CaptainHook.Mail
{
	public class TemplateParser : CommonBase
	{
		enum ParsingState
		{
			Any,
			Start,
			InMacro,
			InMacroArgumentList,
			InQuotedMacroArgument,
			PlainText
		}

		sealed class CurrentState
		{
			TemplateFragment fragment;
			
			public ParsingState State;
			public TemplateFragment Fragment {
				get {
					if (fragment == null)
						throw new InvalidOperationException ("Current state not initialized properly.");

					return fragment;
				}

				set {
					if (value == null)
						throw new ArgumentNullException ("value");

					fragment = value;
				}
			}

			public CurrentState (ParsingState state)
			{
				this.State = state;
			}

			public CurrentState (ParsingState state, TemplateFragment fragment)
				: this (state)
			{
				this.Fragment = fragment;
			}

			public T CastFragment <T> () where T : TemplateFragment
			{
				TemplateFragment fragment = Fragment;
				Type t = fragment.GetType ();
				if (t != typeof (T))
					throw new InvalidOperationException (String.Format ("Expected fragment of type '{0}', found type '{1}' at {2}:{3},{4}",
						typeof (T).FullName, t.FullName, fragment.LineStart, fragment.ColumnStart));

				return Fragment as T;
			}
		}		

		public event EventHandler <FragmentParsedEventArguments> FragmentParsed;

		Stack <CurrentState> stateStack;
		string inputPath;
		int currentLine, currentColumn;

		public TemplateParser (string inputPath)
		{
			this.inputPath = inputPath;
		}
		
		public void Parse ()
		{
			stateStack = new Stack <CurrentState> ();
			stateStack.Push (new CurrentState (ParsingState.Start));
			currentLine = 1;
			currentColumn = -1;
			
			using (var sr = new StreamReader (inputPath, Encoding.UTF8)) {
				Parse (sr);
			}
		}

		void Parse (StreamReader sr)
		{
			int b = sr.Read ();
			char ch;
			CurrentState state, lastState;
			TemplateFragment fragment;

			while (b != -1) {
				ValidateStateStack ();
				state = stateStack.Peek ();
				
				ch = (char)b;
				currentColumn++;

				switch (ch) {
					case '@':
						if (state.State == ParsingState.InMacro) {
							lastState = ZapState (false);
							ValidateStateStack ();
							state = stateStack.Peek ();
							if (state.State == ParsingState.InMacroArgumentList || state.State == ParsingState.InQuotedMacroArgument) {
								var f = state.CastFragment<TemplateFragmentArgument> ();
								f.Fragments.Add (lastState.Fragment);
							} else
								OnFragmentParsed (lastState.Fragment);
						} else {
							if (state.State == ParsingState.PlainText || state.State == ParsingState.Start)
								ZapState ();
							ValidateStateStack ();
							fragment = CreateFragment<TemplateFragmentMacro> ();
							stateStack.Push (new CurrentState (ParsingState.InMacro, fragment));
						}
						break;

					case '(':
						if (state.State == ParsingState.InMacro) {
							fragment = CreateFragment<TemplateFragmentArgument> (f => {
								f.Parent = state.CastFragment<TemplateFragmentMacro> ();
							});
							stateStack.Push (new CurrentState (ParsingState.InMacroArgumentList, fragment));
						} else
							goto default;
						break;

					case ')':
						if (state.State == ParsingState.InMacroArgumentList) {
							lastState = ZapState (false);
							ValidateStateStack (ParsingState.InMacro);
							state = stateStack.Peek ();
							var f = state.CastFragment <TemplateFragmentMacro> ();
							f.Arguments.Add (lastState.CastFragment <TemplateFragmentArgument> ());
						} else
							goto default;

						break;

					case '\r':
						currentColumn--;
						if (state.State == ParsingState.PlainText || state.State == ParsingState.Start)
							goto default;
						else
							throw new InvalidOperationException ("Carriage return characters are allowed only in plain text.");
						
					case '\n':
						currentLine++;
						currentColumn = 0;
						if (state.State == ParsingState.PlainText || state.State == ParsingState.Start)
							goto default;
						else
							throw new InvalidOperationException ("Newline characters are allowed only in plain text.");

					default:
						switch (state.State) {
							default:
								state.Fragment.Append (ch);
								break;

							case ParsingState.Start:
								fragment = CreateFragment <TemplateFragmentPlainText> ();
								fragment.Append (ch);
								stateStack.Push (new CurrentState (ParsingState.PlainText, fragment));
								break;
						}
						break;
				}

				b = sr.Read ();
			}

			ZapState ();
			state = stateStack.Peek ();
			if (state.State != ParsingState.Start)
				throw new InvalidOperationException (String.Format ("Parsing error. Invalid state '{0}' on stack after parsing.", state.State));
		}

		void OnFragmentParsed (TemplateFragment fragment)
		{
			EventHandler <FragmentParsedEventArguments> eh = FragmentParsed;
			if (eh != null)
				eh (this, new FragmentParsedEventArguments (fragment));
		}

		void ValidateStateStack (params ParsingState[] expectedState)
		{
			if (stateStack.Count == 0)
				throw new InvalidOperationException ("Internal error. Parser state stack is empty.");

			CurrentState state = stateStack.Peek ();
			if (state.State == ParsingState.Any)
				throw new InvalidOperationException ("Internal error. Invalid parsing state value found on stack.");

			bool failed = true;
			if (expectedState == null || expectedState.Length == 0)
				return;

			foreach (ParsingState exp in expectedState) {
				if (exp == ParsingState.Any)
					return;
				
				if (state.State == exp) {
					failed = false;
					break;
				}
			}
			
			if (failed)
				throw new InvalidOperationException (String.Format ("Internal error. Unexpected parser state '{0}' at {1}:{2},{3}.", state.State, inputPath, currentLine, currentColumn));
		}

		T CreateFragment <T> () where T : TemplateFragment, new ()
		{
			return CreateFragment <T> (null);
		}
			
		T CreateFragment <T> (Action <T> init) where T : TemplateFragment, new ()
		{
			T fragment = new T () {
				LineStart = currentLine,
				ColumnStart = currentLine,
				InFile = inputPath
			};

			if (init == null)
				return fragment;

			init (fragment);
			return fragment;
		}

		CurrentState ZapState ()
		{
			return ZapState (true);
		}

		CurrentState ZapState (bool triggerOnFragmentParsed)
		{
			CurrentState top = stateStack.Peek ();
			if (top.State == ParsingState.Start)
				return top;

			top = stateStack.Pop ();
			TemplateFragment fragment = top.Fragment;
			fragment.LineEnd = currentLine;
			fragment.ColumnEnd = currentColumn;
			if (triggerOnFragmentParsed)
				OnFragmentParsed (fragment);

			return top;
		}
	}
}
