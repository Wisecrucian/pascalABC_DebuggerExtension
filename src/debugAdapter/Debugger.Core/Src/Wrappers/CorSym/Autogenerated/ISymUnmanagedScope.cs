// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="David Srbecký" email="dsrbecky@gmail.com"/>
//     <version>$Revision: 2201 $</version>
// </file>

// This file is automatically generated - any changes will be lost

#pragma warning disable 1591

namespace Debugger.Wrappers.CorSym
{
	using System;
	
	
	public partial class ISymUnmanagedScope
	{
		
		private Debugger.Interop.CorSym.ISymUnmanagedScope wrappedObject;
		
		internal Debugger.Interop.CorSym.ISymUnmanagedScope WrappedObject
		{
			get
			{
				return this.wrappedObject;
			}
		}
		
		public ISymUnmanagedScope(Debugger.Interop.CorSym.ISymUnmanagedScope wrappedObject)
		{
			this.wrappedObject = wrappedObject;
			ResourceManager.TrackCOMObject(wrappedObject, typeof(ISymUnmanagedScope));
		}
		
		public static ISymUnmanagedScope Wrap(Debugger.Interop.CorSym.ISymUnmanagedScope objectToWrap)
		{
			if ((objectToWrap != null))
			{
				return new ISymUnmanagedScope(objectToWrap);
			} else
			{
				return null;
			}
		}
		
		~ISymUnmanagedScope()
		{
			object o = wrappedObject;
			wrappedObject = null;
			ResourceManager.ReleaseCOMObject(o, typeof(ISymUnmanagedScope));
		}
		
		public bool Is<T>() where T: class
		{
			System.Reflection.ConstructorInfo ctor = typeof(T).GetConstructors()[0];
			System.Type paramType = ctor.GetParameters()[0].ParameterType;
			return paramType.IsInstanceOfType(this.WrappedObject);
		}
		
		public T As<T>() where T: class
		{
			try {
				return CastTo<T>();
			} catch {
				return null;
			}
		}
		
		public T CastTo<T>() where T: class
		{
			return (T)Activator.CreateInstance(typeof(T), this.WrappedObject);
		}
		
		public static bool operator ==(ISymUnmanagedScope o1, ISymUnmanagedScope o2)
		{
			return ((object)o1 == null && (object)o2 == null) ||
			       ((object)o1 != null && (object)o2 != null && o1.WrappedObject == o2.WrappedObject);
		}
		
		public static bool operator !=(ISymUnmanagedScope o1, ISymUnmanagedScope o2)
		{
			return !(o1 == o2);
		}
		
		public override int GetHashCode()
		{
			return base.GetHashCode();
		}
		
		public override bool Equals(object o)
		{
			ISymUnmanagedScope casted = o as ISymUnmanagedScope;
			return (casted != null) && (casted.WrappedObject == wrappedObject);
		}
		
		
		public ISymUnmanagedMethod Method
		{
			get
			{
				return ISymUnmanagedMethod.Wrap(this.WrappedObject.GetMethod());
			}
		}
		
		public ISymUnmanagedScope Parent
		{
			get
			{
				return ISymUnmanagedScope.Wrap(this.WrappedObject.GetParent());
			}
		}
		
		public void GetChildren(uint cChildren, out uint pcChildren, ISymUnmanagedScope[] children)
		{
			Debugger.Interop.CorSym.ISymUnmanagedScope[] array_children = new Debugger.Interop.CorSym.ISymUnmanagedScope[children.Length];
			for (int i = 0; (i < children.Length); i = (i + 1))
			{
				if ((children[i] != null))
				{
					array_children[i] = children[i].WrappedObject;
				}
			}
			this.WrappedObject.GetChildren(cChildren, out pcChildren, array_children);
			for (int i = 0; (i < children.Length); i = (i + 1))
			{
				if ((array_children[i] != null))
				{
					children[i] = ISymUnmanagedScope.Wrap(array_children[i]);
				} else
				{
					children[i] = null;
				}
			}
		}
		
		public uint StartOffset
		{
			get
			{
				return this.WrappedObject.GetStartOffset();
			}
		}
		
		public uint EndOffset
		{
			get
			{
				return this.WrappedObject.GetEndOffset();
			}
		}
		
		public uint LocalCount
		{
			get
			{
				return this.WrappedObject.GetLocalCount();
			}
		}
		
		public void GetLocals(uint cLocals, out uint pcLocals, ISymUnmanagedVariable[] locals)
		{
			Debugger.Interop.CorSym.ISymUnmanagedVariable[] array_locals = new Debugger.Interop.CorSym.ISymUnmanagedVariable[locals.Length];
			for (int i = 0; (i < locals.Length); i = (i + 1))
			{
				if ((locals[i] != null))
				{
					array_locals[i] = locals[i].WrappedObject;
				}
			}
			this.WrappedObject.GetLocals(cLocals, out pcLocals, array_locals);
			for (int i = 0; (i < locals.Length); i = (i + 1))
			{
				if ((array_locals[i] != null))
				{
					locals[i] = ISymUnmanagedVariable.Wrap(array_locals[i]);
				} else
				{
					locals[i] = null;
				}
			}
		}
		
		public void GetNamespaces(uint cNameSpaces, out uint pcNameSpaces, ISymUnmanagedNamespace[] namespaces)
		{
			Debugger.Interop.CorSym.ISymUnmanagedNamespace[] array_namespaces = new Debugger.Interop.CorSym.ISymUnmanagedNamespace[namespaces.Length];
			for (int i = 0; (i < namespaces.Length); i = (i + 1))
			{
				if ((namespaces[i] != null))
				{
					array_namespaces[i] = namespaces[i].WrappedObject;
				}
			}
			this.WrappedObject.GetNamespaces(cNameSpaces, out pcNameSpaces, array_namespaces);
			for (int i = 0; (i < namespaces.Length); i = (i + 1))
			{
				if ((array_namespaces[i] != null))
				{
					namespaces[i] = ISymUnmanagedNamespace.Wrap(array_namespaces[i]);
				} else
				{
					namespaces[i] = null;
				}
			}
		}
	}
}

#pragma warning restore 1591
