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
using System.Threading;

using CaptainHook.Utils;

namespace CaptainHook.Base
{
	public class CommonBase
	{
		const int rateLimitSleep = 60 * 1000;
		
		protected static void Log (LogSeverity severity, string format, params object[] parms)
		{
			if (severity == LogSeverity.Debug && !Config.Instance.Debug)
				return;

			Console.Error.Write ("*CaptainHook {0}*: ", severity);
			Console.Error.WriteLine (format, parms);
		}

		protected static void Log (Exception ex)
		{
			Log (ex, null);
			if (Config.Instance.Debug) {
				Console.Error.WriteLine ("[DEBUG] Full exception trace:");
				Console.Error.WriteLine (ex);
				Console.Error.WriteLine ();
			}
		}

		// If format is specified, the following parameters are passed:
		//
		//  {0} - exception object
		//  {1} - exception type
		//  {2} - exception message
		//  {3} - exception stack trace
		//
		// Your paramters, if any, start at {4}
		//
		protected static void Log (Exception ex, string format, params object[] parms)
		{
			if (ex == null)
				return;
			
			if (String.IsNullOrEmpty (format))
				Log (LogSeverity.Error, "Exception '{0}' caught. {1}", ex.GetType (), ex.Message);
			else {
				object[] newParams = new object [4 + (parms != null ? parms.Length : 0)];
				newParams [0] = ex;
				newParams [1] = ex.GetType ();
				newParams [2] = ex.Message;
				newParams [3] = ex.StackTrace;

				if (parms != null) {
					for (int i = 0; i < parms.Length; i++)
						newParams [i + 4] = parms [i];
				}
				
				Log (LogSeverity.Error, format, newParams);
			}
		}

		protected static void Throttle (WebClient client)
		{
			string rate_limit = client.ResponseHeaders ["X-RateLimit-Remaining"];
			if (String.IsNullOrEmpty (rate_limit))
				return;
			
			string current_rate_limit = client.ResponseHeaders ["X-RateLimit-Limit"];
			if (String.IsNullOrEmpty (current_rate_limit))
				current_rate_limit = "60";
			Log (LogSeverity.Debug, "GitHub rate limit: {1}; remaining: {0}", rate_limit, current_rate_limit);
			uint limit;
			if (!UInt32.TryParse (rate_limit, out limit))
				return;
			
			if (limit > 1)
				return;
			
			Log (LogSeverity.Info, "[{0}] Throttling the current request, GitHub rate limit exceeded. Sleeping for {1}s", Thread.CurrentThread.ManagedThreadId, rateLimitSleep / 1000);
			System.Threading.Thread.Sleep (rateLimitSleep);
		}
	}
}
