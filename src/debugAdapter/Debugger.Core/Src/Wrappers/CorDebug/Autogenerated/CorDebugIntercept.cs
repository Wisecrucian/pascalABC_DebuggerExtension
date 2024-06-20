// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="David Srbecký" email="dsrbecky@gmail.com"/>
//     <version>$Revision: 2077 $</version>
// </file>

// This file is automatically generated - any changes will be lost

#pragma warning disable 1591

namespace Debugger.Wrappers.CorDebug
{
	using System;
	
	
	public enum CorDebugIntercept : int
	{
		
		INTERCEPT_NONE = 0,
		
		INTERCEPT_CLASS_INIT = 1,
		
		INTERCEPT_EXCEPTION_FILTER = 2,
		
		INTERCEPT_SECURITY = 4,
		
		INTERCEPT_CONTEXT_POLICY = 8,
		
		INTERCEPT_INTERCEPTION = 16,
		
		INTERCEPT_ALL = 65535,
	}
}

#pragma warning restore 1591
