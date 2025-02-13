using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JavaObjectTest : JavaVMFixture
	{
#if !NO_GC_BRIDGE_SUPPORT
		[Test]
		public void JavaReferencedInstanceSurvivesCollection ()
		{
			Console.WriteLine ("JavaReferencedInstanceSurvivesCollection");
			using (var t = new JniType ("java/lang/Object")) {
				var oldHandle = IntPtr.Zero;
				var array     = new JavaObjectArray<JavaObject> (1);
				FinalizerHelpers.PerformNoPinAction (() => {
						var v       = new JavaObject ();
						oldHandle   = v.PeerReference.Handle;
						array [0] = v;
				});
				JniEnvironment.Runtime.ValueManager.CollectPeers ();
				GC.WaitForPendingFinalizers ();
				GC.WaitForPendingFinalizers ();
				var first = array [0];
				Assert.IsNotNull (JniRuntime.CurrentRuntime.ValueManager.PeekValue (first.PeerReference));
				var f = first.PeerReference;
				var o = (JavaObject) JniRuntime.CurrentRuntime.ValueManager.GetValue (ref f, JniObjectReferenceOptions.Copy);
				Assert.AreSame (first, o);
				if (oldHandle != o.PeerReference.Handle) {
					Console.WriteLine ("Yay, object handle changed; value survived a GC!");
				} else {
					Console.WriteLine ("What is this, Android pre-ICS?!");
				}
				o.Dispose ();
				array.Dispose ();
			}
		}
#endif  // !NO_GC_BRIDGE_SUPPORT

		[Test]
		public void UnregisterFromRuntime ()
		{
			int registeredCount = JniRuntime.CurrentRuntime.ValueManager.GetSurfacedPeers ().Count;
			JniObjectReference l;
			JavaObject o;
			using (o = new JavaObject ()) {
				l   = o.PeerReference.NewLocalRef ();
				Assert.AreEqual (JniObjectReferenceType.Global, o.PeerReference.Type);
				Assert.AreEqual (registeredCount+1, JniRuntime.CurrentRuntime.ValueManager.GetSurfacedPeers ().Count);
				Assert.IsNotNull (JniRuntime.CurrentRuntime.ValueManager.PeekValue (l));
				Assert.AreNotSame (l, o.PeerReference);
				Assert.AreSame (o, JniRuntime.CurrentRuntime.ValueManager.PeekValue (l));
			}
			Assert.AreEqual (registeredCount, JniRuntime.CurrentRuntime.ValueManager.GetSurfacedPeers ().Count);
			Assert.IsNull (JniRuntime.CurrentRuntime.ValueManager.PeekValue (l));
			JniObjectReference.Dispose (ref l);
			Assert.Throws<ObjectDisposedException> (() => o.UnregisterFromRuntime ());
		}

		[Test]
		public void RegisterWithVM_PermitsAliases ()
		{
			using (var original = new JavaObject ()) {
				var p       = original.PeerReference;
				var alias   = new JavaObject (ref p, JniObjectReferenceOptions.Copy);
				alias.Dispose ();
			}
		}

#if !NO_GC_BRIDGE_SUPPORT
		[Test]
		public void UnreferencedInstanceIsCollected ()
		{
			JniObjectReference  oldHandle = new JniObjectReference ();
			WeakReference r = null;
			FinalizerHelpers.PerformNoPinAction (() => {
					var v     = new JavaObject ();
					oldHandle = v.PeerReference.NewWeakGlobalRef ();
					r         = new WeakReference (v);
			});
			JniEnvironment.Runtime.ValueManager.CollectPeers ();
			GC.WaitForPendingFinalizers ();
			GC.WaitForPendingFinalizers ();
			Assert.IsFalse (r.IsAlive);
			Assert.IsNull (r.Target);
			Assert.IsNull (JniRuntime.CurrentRuntime.ValueManager.PeekValue (oldHandle));
			JniObjectReference.Dispose (ref oldHandle);
		}
#endif  // !NO_GC_BRIDGE_SUPPORT

		[Test]
		public void Dispose ()
		{
			var d = false;
			var f = false;
			var o = new JavaDisposedObject (() => d = true, () => f = true);
			o.Dispose ();
			Assert.IsTrue (d);
			Assert.IsFalse (f);
		}

#if !NO_GC_BRIDGE_SUPPORT
		[Test]
		public void Dispose_Finalized ()
		{
			var d = false;
			var f = false;
			FinalizerHelpers.PerformNoPinAction (() => {
				FinalizerHelpers.PerformNoPinAction (() => {
					var v     = new JavaDisposedObject (() => d = true, () => f = true);
					GC.KeepAlive (v);
				});
				JniEnvironment.Runtime.ValueManager.CollectPeers ();
			});
			JniEnvironment.Runtime.ValueManager.CollectPeers ();
			GC.WaitForPendingFinalizers ();
			Assert.IsFalse (d);
			Assert.IsTrue (f);
		}
#endif  // !NO_GC_BRIDGE_SUPPORT

		[Test]
		public void ObjectDisposed ()
		{
			var o = new JavaObject ();
			o.Dispose ();

			// These should not throw
			var h = o.PeerReference;
			var p = o.JniPeerMembers;

			// These should throw
			Assert.Throws<ObjectDisposedException> (() => o.GetHashCode ());
			Assert.Throws<ObjectDisposedException> (() => o.UnregisterFromRuntime ());
			Assert.Throws<ObjectDisposedException> (() => o.ToString ());
			Assert.Throws<ObjectDisposedException> (() => o.Equals (o));
		}

		[Test]
		public unsafe void Ctor ()
		{
			using (var t = new JniType ("java/lang/Object")) {
				var c = t.GetConstructor ("()V");
				var lref = t.NewObject (c, null);
				Assert.IsTrue (lref.IsValid);
				using (var o = new JavaObject (ref lref, JniObjectReferenceOptions.Copy)) {
					Assert.IsTrue (lref.IsValid);
					Assert.AreNotSame (lref, o.PeerReference);
				}
				using (var o = new JavaObject (ref lref, JniObjectReferenceOptions.CopyAndDispose)) {
					Assert.IsFalse (lref.IsValid);
					Assert.AreNotSame (lref, o.PeerReference);
				}
			}
		}

		[Test]
		public void Ctor_Exceptions ()
		{
			var r   = new JniObjectReference ();
			Assert.Throws<ArgumentException> (() => new JavaObject (ref r, JniObjectReferenceOptions.CopyAndDispose));

			// Note: This may break if/when JavaVM provides "default"
			Assert.Throws<NotSupportedException> (() => new JavaObjectWithNoJavaPeer ());
#if __ANDROID__
			Assert.Throws<Java.Lang.ClassNotFoundException> (() => new JavaObjectWithMissingJavaPeer ()).Dispose ();
#else   // !__ANDROID__
			Assert.Throws<JavaException> (() => new JavaObjectWithMissingJavaPeer ()).Dispose ();
#endif  // !__ANDROID__
		}

		[Test]
		public void CrossThreadSharingRequresRegistration ()
		{
			JavaObject o = null;
			FinalizerHelpers.PerformNoPinAction (() => {
					o = new JavaObject ();
			});
			o.ToString ();
			o.Dispose ();
		}

		[Test]
		public void NestedDisposeInvocations ()
		{
			var value = new MyDisposableObject ();
			value.Dispose ();
			value.Dispose ();
		}

		[Test]
		public void DisposeAccessesThis ()
		{
			var value = new GetThis ();
			value.Dispose ();
			value.Dispose ();
		}
	}

	class JavaObjectWithNoJavaPeer : JavaObject {
	}

	[JniTypeSignature (JniTypeName)]
	class JavaObjectWithMissingJavaPeer : JavaObject {
		internal    const   string  JniTypeName = "__this__/__type__/__had__/__better__/__not__/__Exist__";
	}

	[JniTypeSignature ("java/lang/Object")]
	class JavaDisposedObject : JavaObject {

		public Action   OnDisposed;
		public Action   OnFinalized;

		public JavaDisposedObject (Action disposed, Action finalized)
		{
			OnDisposed  = disposed;
			OnFinalized = finalized;
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing)
				OnDisposed ();
			else
				OnFinalized ();
		}
	}

	[JniTypeSignature ("java/lang/Object")]
	class MyDisposableObject : JavaObject {
		bool _isDisposed;

		public MyDisposableObject ()
		{
		}

		protected override void Dispose (bool disposing)
		{
			if (_isDisposed) {
				return;
			}
			_isDisposed = true;
			if (this.PeerReference.IsValid)
				this.Dispose ();
			base.Dispose (disposing);
		}
	}
}

