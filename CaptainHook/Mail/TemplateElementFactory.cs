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
using System.Reflection;
using System.Text;

using CaptainHook.Base;
using CaptainHook.GitHub;
using CaptainHook.Utils;

namespace CaptainHook.Mail
{
	public class TemplateElementFactory <TData> : CommonBase
	{
		sealed class MacroMapEntry
		{
			public Type ValidFor;
			public Func <TemplateFragmentMacro, TemplateElement> Processor;

			public MacroMapEntry (Type validFor, Func<TemplateFragmentMacro, TemplateElement> processor)
			{
				this.ValidFor = validFor;
				this.Processor = processor;
			}
		}

		static readonly string newline = Environment.NewLine;
		static readonly char[] argumentSplitChars = { ',' };
		
		Dictionary <string, MacroMapEntry> macroMap;
		string basePath;
		string commitSourceID;
		
		
		public TemplateElementFactory (string basePath, string commitSourceID)
		{
			this.basePath = basePath;
			this.commitSourceID = commitSourceID;

			macroMap = new Dictionary<string, MacroMapEntry> (StringComparer.Ordinal) {
				// Global macros
				{"Header", new MacroMapEntry (null, Macro_Global_Header) },
				{"Version", new MacroMapEntry (null, Macro_Global_Version) },
				{"IfDifferent", new MacroMapEntry (null, Macro_Global_IfDifferent) },

				// Push macros
				{"Subject", new MacroMapEntry (typeof (Push), Macro_Push_Subject) },
				{"FirstCommit", new MacroMapEntry (typeof (Push), Macro_Push_FirstCommit) },
				{"FirstCommit.MessageSummary", new MacroMapEntry (typeof (Push), Macro_Push_FirstCommit_MessageSummary) },
				{"AffectedDirectories", new MacroMapEntry (typeof (Push), Macro_Push_AffectedDirectories) },
				{"NumberOfCommits", new MacroMapEntry (typeof (Push), Macro_Push_NumberOfCommits) },

				// Commit macros
				{"ChangedPaths", new MacroMapEntry (typeof (Commit), Macro_Commit_ChangedPaths)},
				{"AddedPaths", new MacroMapEntry (typeof (Commit), Macro_Commit_AddedPaths)},
				{"RemovedPaths", new MacroMapEntry (typeof (Commit), Macro_Commit_RemovedPaths)},
				{"FullDiff", new MacroMapEntry (typeof (Commit), Macro_Commit_FullDiff)}
			};
		}

		public TemplateElement GetElement (TemplateFragment fragment)
		{
			if (fragment == null)
				throw new ArgumentNullException ("fragment");

			TemplateFragmentPlainText text = fragment as TemplateFragmentPlainText;
			if (text != null)
				return new TemplateElementText (text.Data);

			TemplateElement ret = null;
			TemplateFragmentMacro macro = fragment as TemplateFragmentMacro;
			if (macro != null)
				ret = ProcessMacro (macro);

			if (ret == null)
				throw new InvalidOperationException (
					String.Format ("Failed to generate a template element from fragment {0} at {1}:{2},{3}",
					fragment, fragment.InFile, fragment.LineStart, fragment.ColumnStart)
				);

			return ret;
		}

		TemplateElementArgument GetElementArgument (TemplateFragmentArgument arg)
		{
			var components = new List<TemplateElement> ();

			foreach (TemplateFragment fragment in arg.Fragments)
				components.Add (GetElement (fragment));

			return new TemplateElementArgument (components);
		}

		List<TemplateElementArgument> GetElementArgumentList (TemplateFragmentMacro fragment)
		{
			List<TemplateElementArgument> arguments = null;

			if (fragment.HasArguments) {
				arguments = new List<TemplateElementArgument> ();
				foreach (TemplateFragmentArgument arg in fragment.Arguments)
					arguments.Add (GetElementArgument (arg));
			}

			if (arguments != null && arguments.Count > 0)
				return arguments;

			return null;
		}

		Func<TemplateFragmentMacro, TemplateElement> GetProcessor (string name)
		{
			MacroMapEntry mapEntry;
			if (macroMap.TryGetValue (name, out mapEntry) && mapEntry != null && mapEntry.Processor != null &&
				(mapEntry.ValidFor == null || mapEntry.ValidFor == typeof(TData)))
				return mapEntry.Processor;

			return null;
		}

		TemplateElement ProcessMacro (TemplateFragmentMacro macro)
		{
			string name = macro.MacroName;
			if (name.StartsWith ("this.", StringComparison.Ordinal))
				name = name.Substring (5);

			Func<TemplateFragmentMacro, TemplateElement> processor = GetProcessor (name);
			if (processor != null)
				return processor (macro);

			int dot = name.IndexOf ('.');
			if (dot != -1) {
				string nameLead = name.Substring (0, dot);
				processor = GetProcessor (nameLead);
				if (processor != null)
					return processor (macro);
			}
			var ret = new TemplateElementPropertyReference<TData> (name);
			if (ret.IsCollection) {
				Type tt = typeof(Template<>);
				tt = tt.MakeGenericType (new Type[] { GetCollectionElementType (ret.PropertyType) });
				ITemplate template = Activator.CreateInstance (tt, new object[] { basePath, commitSourceID }) as ITemplate;
				if (!template.Compile ())
					return null;

				ret.Template = template;
			}

			return ret;
		}

		Type GetCollectionElementType (Type type)
		{
			if (!type.IsGenericType)
				throw new ArgumentException ("Type must be a generic collection.", "type");

			if (typeof(IDictionary<, >).IsAssignableFrom (type)) {
				Type kvt = typeof(KeyValuePair<, >);
				return kvt.MakeGenericType (type.GetGenericArguments ());
			}

			return type.GetGenericArguments ()[0];
		}

		TemplateElement Macro_Global_Header (TemplateFragmentMacro fragment)
		{
			List<TemplateElementArgument> arguments = GetElementArgumentList (fragment);
			return new TemplateElementMailHeader (arguments) {
				SkipNewlineIfLineEmpty = true
			};
		}

		TemplateElement Macro_Global_Version (TemplateFragmentMacro fragment)
		{
			return new TemplateElementText (Config.Instance.Version);
		}

		TemplateElement Macro_Global_IfDifferent (TemplateFragmentMacro fragment)
		{
			return new TemplateElementSynthetic<TData> (GetElementArgumentList (fragment), (TData data, List<TemplateElementArgument> args) =>
			{
				if (args == null || args.Count == 0)
					return null;

				var sb = new StringBuilder ();
				foreach (TemplateElementArgument a in args)
					sb.Append (a.Generate (data));

				string argstr = sb.ToString ();
				if (argstr.Length == 0)
					return null;

				string[] arglist = argstr.Split (argumentSplitChars);
				if (arglist.Length < 3) {
					Log (LogSeverity.Error, "Not enough arguments for the IfDifferent macro (expected 3, got {0}", arglist.Length);
					return null;
				}

				string heading = arglist[0];
				string left = arglist[1];
				string right = String.Join (",", arglist, 2, arglist.Length - 2);

				if (String.Compare (left, right, StringComparison.Ordinal) == 0)
					return null;

				return heading + left;
			}) {
				SkipNewlineIfLineEmpty = true
			};
		}

		TemplateElement Macro_Push_Subject (TemplateFragmentMacro fragment)
		{
			List<TemplateElementArgument> arguments = GetElementArgumentList (fragment);
			return new TemplateElementMailHeader ("Subject", arguments) {
				SkipNewlineIfLineEmpty = true,
				IgnoreEqualsSign = true
			};
		}

		string ShortenString (string data, List<TemplateElementArgument> args, int defvalue)
		{
			if (String.IsNullOrEmpty (data))
				return data;

			int nchars = defvalue;
			if (args != null && args.Count > 0) {
				TemplateElementArgument arg = args[0];
				try {
					int result;
					if (Int32.TryParse (arg.Generate (data), out result))
						nchars = result;
				} catch {
					// ignore
				}
			}

			if (nchars < 0)
				return data;

			int len = data.Length;
			if (nchars >= len)
				return data;
			int newline = data.IndexOf ('\n');
			if (newline > -1 && newline < nchars)
				nchars = newline;

			return data.Substring (0, nchars);
		}

		TemplateElement Macro_Push_FirstCommit (TemplateFragmentMacro fragment)
		{
			string macroName = fragment.MacroName;
			if (!macroName.StartsWith ("FirstCommit", StringComparison.Ordinal))
				throw new ArgumentException ("Macro name must start with FirstCommit", "fragment");

			string propName = macroName.Substring (12);
			if (String.IsNullOrEmpty (propName))
				throw new InvalidOperationException ("A non-empty property name is required.");

			var propref = new TemplateElementPropertyReference<Commit> (propName);
			return new TemplateElementSynthetic<Push> (GetElementArgumentList (fragment), (Push data, List<TemplateElementArgument> args) =>
			{
				List<Commit> commits = data.Commits;
				if (commits == null || commits.Count == 0)
					return null;

				Commit first = commits[0];
				return ShortenString(propref.Generate (first), args, -1);
			}) {
				SkipNewlineIfLineEmpty = true
			};
		}

		TemplateElement Macro_Push_FirstCommit_MessageSummary (TemplateFragmentMacro fragment)
		{
			string macroName = fragment.MacroName;
			if (String.Compare ("FirstCommit.MessageSummary", macroName, StringComparison.Ordinal) != 0)
				throw new ArgumentException ("Macro name must be 'FirstCommit.MessageSummary'", "fragment");

			var propref = new TemplateElementPropertyReference<Commit> ("Message");
			return new TemplateElementSynthetic<Push> (GetElementArgumentList (fragment), (Push data, List<TemplateElementArgument> args) =>
			{
				List<Commit> commits = data.Commits;
				if (commits == null || commits.Count == 0)
					return null;

				Commit first = commits[0];
				string ret = propref.Generate (first);
				if (String.IsNullOrEmpty (ret))
					return null;

				return ShortenString (ret, args, 72);
			}) {
				SkipNewlineIfLineEmpty = true
			};
		}

		void AddUniqueDirectoriesToCache (List<string> paths, IDictionary<string, bool> cache)
		{
			if (paths == null || paths.Count == 0)
				return;

			string dir;
			int lastSlash;
			foreach (string p in paths) {
				if (String.IsNullOrEmpty (p))
					continue;

				lastSlash = p.LastIndexOf ('/');
				if (lastSlash == -1)
					dir = "/";
				else
					dir = p.Substring (0, lastSlash + 1);

				if (cache.ContainsKey (dir))
					continue;

				cache.Add (dir, true);
			}
		}

		TemplateElement Macro_Push_AffectedDirectories (TemplateFragmentMacro fragment)
		{
			string macroName = fragment.MacroName;
			if (String.Compare ("AffectedDirectories", macroName, StringComparison.Ordinal) != 0)
				throw new ArgumentException ("Macro name must be 'AffectedDirectories'", "fragment");

			return new TemplateElementListMailHeader<Push> (GetElementArgumentList (fragment), (Push data, List<string> values) =>
			{
				List<Commit> commits = data.Commits;
				if (commits == null || commits.Count == 0)
					return;

				var paths = new SortedDictionary<string, bool> (StringComparer.Ordinal);
				foreach (Commit c in commits) {
					AddUniqueDirectoriesToCache (c.Added, paths);
					AddUniqueDirectoriesToCache (c.Modified, paths);
					AddUniqueDirectoriesToCache (c.Removed, paths);
				}

				foreach (string dir in paths.Keys)
					values.Add (dir);
			}) {
				SkipNewlineIfLineEmpty = true
			};
		}

		TemplateElement Macro_Push_NumberOfCommits (TemplateFragmentMacro fragment)
		{
			string macroName = fragment.MacroName;
			if (String.Compare ("NumberOfCommits", macroName, StringComparison.Ordinal) != 0)
				throw new ArgumentException ("Macro name must be 'NumberOfCommits'", "fragment");

			return new TemplateElementSynthetic<Push> (GetElementArgumentList (fragment), (Push data, List<TemplateElementArgument> args) =>
			{
				List<Commit> commits = data.Commits;
				if (commits == null || commits.Count == 0)
					return null;

				if (args == null || args.Count == 0)
					return null;

				var sb = new StringBuilder ();
				foreach (TemplateElementArgument a in args)
					sb.Append (a.Generate (data));

				string arg = sb.ToString ();
				int comma = arg.IndexOf (',');
				int min = 2;

				if (comma != -1) {
					int v;

					if (Int32.TryParse (arg.Substring (0, comma), out v)) {
						min = v;
						arg = arg.Substring (comma + 1);
					}
				}

				if (commits.Count < min)
					return null;

				return arg;
			}) {
				SkipNewlineIfLineEmpty = true
			};
		}

		TemplateElement Macro_Commit_ChangedPaths (TemplateFragmentMacro fragment)
		{
			return new TemplateElementSynthetic<Commit> (GetElementArgumentList (fragment), (Commit data, List<TemplateElementArgument> args) =>
				{
				List<string> modified = data.Modified;
				if (modified == null || modified.Count == 0)
					return null;

				var sb = new StringBuilder ();
				if (args == null)
					sb.AppendLine ("Changed paths:");
				else
					sb.AppendLine (args[0].Generate (data));

				foreach (string file in modified)
					sb.Append (" M " + file + newline);

				return sb.ToString ();
			}) {
				SkipNewlineIfLineEmpty = true
			};
		}

		TemplateElement Macro_Commit_AddedPaths (TemplateFragmentMacro fragment)
		{
			return new TemplateElementSynthetic<Commit> (GetElementArgumentList (fragment), (Commit data, List<TemplateElementArgument> args) =>
			{
				List<string> added = data.Added;
				if (added == null || added.Count == 0)
					return null;

				var sb = new StringBuilder ();
				if (args == null)
					sb.AppendLine ("Added paths:");
				else
					sb.AppendLine (args[0].Generate (data));

				foreach (string file in added)
					sb.Append (" A " + file + newline);

				return sb.ToString ();
			}) {
				SkipNewlineIfLineEmpty = true
			};
		}

		TemplateElement Macro_Commit_RemovedPaths (TemplateFragmentMacro fragment)
		{
			return new TemplateElementSynthetic<Commit> (GetElementArgumentList (fragment), (Commit data, List<TemplateElementArgument> args) =>
			{
				List<string> removed = data.Removed;
				if (removed == null || removed.Count == 0)
					return null;

				var sb = new StringBuilder ();
				if (args == null)
					sb.AppendLine ("Removed paths:");
				else
					sb.AppendLine (args[0].Generate (data));

				foreach (string file in removed)
					sb.Append (" D " + file + newline);

				return sb.ToString ();
			}) {
				SkipNewlineIfLineEmpty = true
			};
		}

		TemplateElement Macro_Commit_FullDiff (TemplateFragmentMacro fragment)
		{
			return new TemplateElementSynthetic<Commit> (GetElementArgumentList (fragment), (Commit data, List<TemplateElementArgument> args) =>
			{
				CommitWithDiff diff = data.Diff;
				if (diff != null)
					return diff.GetFullDiff ();

				return null;
			}) {
				SkipNewlineIfLineEmpty = true
			};
		}
	}
}

