using System;
using System.Collections.Generic;
using Java.Interop;

namespace Test.ME {

	// Metadata.xml XPath class reference: path="/api/package[@name='test.me']/class[@name='GenericObjectPropertyImplementation']"
	[global::Java.Interop.JniTypeSignature ("test/me/GenericObjectPropertyImplementation", GenerateJavaPeer=false)]
	public partial class GenericObjectPropertyImplementation : global::Java.Lang.Object, global::Test.ME.IGenericPropertyInterface {
		static readonly JniPeerMembers _members = new JniPeerMembers ("test/me/GenericObjectPropertyImplementation", typeof (GenericObjectPropertyImplementation));

		[global::System.Diagnostics.DebuggerBrowsable (global::System.Diagnostics.DebuggerBrowsableState.Never)]
		[global::System.ComponentModel.EditorBrowsable (global::System.ComponentModel.EditorBrowsableState.Never)]
		public override global::Java.Interop.JniPeerMembers JniPeerMembers {
			get { return _members; }
		}

		protected GenericObjectPropertyImplementation (ref JniObjectReference reference, JniObjectReferenceOptions options) : base (ref reference, options)
		{
		}

		// Metadata.xml XPath constructor reference: path="/api/package[@name='test.me']/class[@name='GenericObjectPropertyImplementation']/constructor[@name='GenericObjectPropertyImplementation' and count(parameter)=0]"
		public unsafe GenericObjectPropertyImplementation () : base (ref *InvalidJniObjectReference, JniObjectReferenceOptions.None)
		{
			const string __id = "()V";

			if (PeerReference.IsValid)
				return;

			try {
				var __r = _members.InstanceMethods.StartCreateInstance (__id, ((object) this).GetType (), null);
				Construct (ref __r, JniObjectReferenceOptions.CopyAndDispose);
				_members.InstanceMethods.FinishCreateInstance (__id, this, null);
			} finally {
			}
		}

		public virtual unsafe global::Java.Lang.Object Object {
			// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/class[@name='GenericObjectPropertyImplementation']/method[@name='getObject' and count(parameter)=0]"
			get {
				const string __id = "getObject.()Ljava/lang/Object;";
				try {
					var __rm = _members.InstanceMethods.InvokeVirtualObjectMethod (__id, this, null);
					return global::Java.Interop.JniEnvironment.Runtime.ValueManager.GetValue<global::Java.Lang.Object> (ref __rm, JniObjectReferenceOptions.CopyAndDispose);
				} finally {
				}
			}
			// Metadata.xml XPath method reference: path="/api/package[@name='test.me']/class[@name='GenericObjectPropertyImplementation']/method[@name='setObject' and count(parameter)=1 and parameter[1][@type='java.lang.Object']]"
			set {
				const string __id = "setObject.(Ljava/lang/Object;)V";
				try {
					JniArgumentValue* __args = stackalloc JniArgumentValue [1];
					__args [0] = new JniArgumentValue (value);
					_members.InstanceMethods.InvokeVirtualVoidMethod (__id, this, __args);
				} finally {
					global::System.GC.KeepAlive (value);
				}
			}
		}

	}
}
