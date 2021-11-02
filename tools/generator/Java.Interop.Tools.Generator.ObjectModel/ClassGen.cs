using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Java.Interop.Tools.Generator;
using Java.Interop.Tools.TypeNameMappings;
using Xamarin.Android.Binder;

namespace MonoDroid.Generation
{
	public class ClassGen : GenBase
	{
		bool fill_explicit_implementation_started;

		public List<Ctor> Ctors { get; private set; } = new List<Ctor> ();

		public ClassGen (GenBaseSupport support) : base (support)
		{
			InheritsObject = true;

			if (Namespace == "Java.Lang" && (Name == "Object" || Name == "Throwable"))
				InheritsObject = false;

			DefaultValue = "IntPtr.Zero";
			NativeType = "IntPtr";
		}

		static void AddNestedInterfaceTypes (GenBase type, List<InterfaceExtensionInfo> nestedInterfaces)
		{
			foreach (var nt in type.NestedTypes) {
				if (nt is InterfaceGen ni)
					nestedInterfaces.Add (new InterfaceExtensionInfo {
						DeclaringType = type.FullName.Substring (type.Namespace.Length + 1).Replace (".", "_"),
						Type = ni
					});
				else
					AddNestedInterfaceTypes (nt, nestedInterfaces);
			}
		}

		public override ClassGen BaseGen =>
			(base_symbol is GenericSymbol ? (base_symbol as GenericSymbol).Gen : base_symbol) as ClassGen;

		public string BaseType { get; set; }

		public bool ContainsCtor (string jni_sig) => Ctors.Any (c => c.JniSignature == jni_sig);

		public bool ContainsNestedType (GenBase gen)
		{
			if (BaseGen != null && BaseGen.ContainsNestedType (gen))
				return true;

			return HasNestedType (gen.Name);
		}

		public List<string> ExplicitlyImplementedInterfaceMethods { get; } = new List<string> (); // do not initialize here; see FixupMethodOverides()

		public override void FixupAccessModifiers (CodeGenerationOptions opt)
		{
			while (!IsAnnotation && !string.IsNullOrEmpty (BaseType)) {
				if (opt.SymbolTable.Lookup (BaseType) is ClassGen baseClass && RawVisibility == "public" && baseClass.RawVisibility != "public") {
					//Skip the BaseType and copy over any "missing" methods
					foreach (var baseMethod in baseClass.Methods) {
						var method = Methods.FirstOrDefault (m => m.Matches (baseMethod));
						if (method == null)
							Methods.Add (baseMethod);
					}
					BaseType = baseClass.BaseType;
				} else {
					break;
				}
			}

			base.FixupAccessModifiers (opt);
		}

		public override void FixupExplicitImplementation ()
		{
			if (fill_explicit_implementation_started)
				return; // already done.
			fill_explicit_implementation_started = true;
			if (BaseGen != null && BaseGen.ExplicitlyImplementedInterfaceMethods == null)
				BaseGen.FixupExplicitImplementation ();

			foreach (InterfaceGen iface in GetAllDerivedInterfaces ()) {
				if (iface.IsGeneric) {
					bool skip = false;
					foreach (ISymbol isym in Interfaces) {
						if (isym is GenericSymbol gs && gs.IsConcrete && gs.Gen == iface)
							skip = true;
					}
					if (skip)
						continue; // we don't handle it here; generic interface methods are generated in different manner.
				}
				if (BaseGen != null && BaseGen.GetAllDerivedInterfaces ().Contains (iface))
					continue; // no need to fill members for already-implemented-in-base-class iface.
				foreach (var m in iface.Methods.Where (m => !ContainsMethod (m, false, false))) {
					string sig = m.GetSignature ();
					bool doExplicitly = false;
					if (IsCovariantMethod (m))
						doExplicitly = true;
					else if (m.IsGeneric)
						doExplicitly = true;
					if (doExplicitly)
						ExplicitlyImplementedInterfaceMethods.Add (sig);
				}
			}

			// Keep in sync with Generate() that generates explicit iface method impl.
			foreach (ISymbol isym in Interfaces) {
				if (isym is GenericSymbol) {
					GenericSymbol gs = isym as GenericSymbol;
					if (gs.IsConcrete) {
						foreach (Method m in gs.Gen.Methods)
							if (m.IsGeneric) {
								ExplicitlyImplementedInterfaceMethods.Add (m.GetSignature ());
							}
					}
				}
			}

			foreach (var nt in NestedTypes)
				nt.FixupExplicitImplementation ();
		}

		public override string FromNative (CodeGenerationOptions opt, string varname, bool owned) =>
			$"global::Java.Lang.Object.GetObject<{opt.GetOutputName (FullName)}> ({varname}, {(owned ? "JniHandleOwnership.TransferLocalRef" : "JniHandleOwnership.DoNotTransfer")})";

		public bool FromXml { get; set; }

		public override void Generate (CodeGenerationOptions opt, GenerationInfo gen_info)
		{
			using (var sw = gen_info.OpenStream (opt.GetFileName (FullName))) {
				sw.WriteLine ("using System;");
				sw.WriteLine ("using System.Collections.Generic;");
				sw.WriteLine ("using Android.Runtime;");
				if (opt.CodeGenerationTarget != CodeGenerationTarget.XamarinAndroid) {
					sw.WriteLine ("using Java.Interop;");
				}
				sw.WriteLine ();
				var hasNamespace = !string.IsNullOrWhiteSpace (Namespace);
				if (hasNamespace) {
					sw.WriteLine ("namespace {0} {{", Namespace);
					sw.WriteLine ();
				}

				var generator = opt.CreateCodeGenerator (sw);
				generator.WriteType (this, hasNamespace ? "\t" : string.Empty, gen_info);

				if (hasNamespace) {
					sw.WriteLine ("}");
				}
			}
		}

		public static void GenerateEnumList (GenerationInfo gen_info)
		{
			using (var sw = new StreamWriter (File.Create (Path.Combine (gen_info.CSharpDir, "enumlist")))) {
				foreach (string e in gen_info.Enums.OrderBy (p => p, StringComparer.OrdinalIgnoreCase))
					sw.WriteLine (e);
			}
		}

		protected override bool GetEnumMappedMemberInfo ()
		{
			foreach (var m in Ctors)
				return true;

			return base.GetEnumMappedMemberInfo ();
		}

		internal IEnumerable<InterfaceExtensionInfo> GetNestedInterfaceTypes ()
		{
			var nestedInterfaces = new List<InterfaceExtensionInfo> ();
			AddNestedInterfaceTypes (this, nestedInterfaces);
			return nestedInterfaces;
		}

		public bool InheritsObject { get; set; }

		public bool IsAbstract { get; set; }

		public bool IsExplicitlyImplementedMethod (string sig)
		{
			for (var c = this; c != null; c = c.BaseGen)
				if (c.ExplicitlyImplementedInterfaceMethods.Contains (sig))
					return true;
			return false;
		}

		public bool IsFinal { get; set; }

		public bool NeedsNew { get; set; }

		protected override bool OnValidate (CodeGenerationOptions opt, GenericParameterDefinitionList type_params, CodeGeneratorContext context)
		{
			if (validated)
				return IsValid;

			validated = true;

			if (!support.OnValidate (opt)) {
				IsValid = false;
				return false;
			}

			// We're validating this in prior to BaseType.
			if (TypeParameters != null && !TypeParameters.Validate (opt, type_params, context)) {
				IsValid = false;
				return false;
			}

			if (char.IsNumber (Name [0])) {
				// it is an anonymous class which does not need output.
				IsValid = false;
				return false;
			}

			base_symbol = IsAnnotation ? opt.SymbolTable.Lookup ("java.lang.Object") : BaseType != null ? opt.SymbolTable.Lookup (BaseType) : null;
			if (base_symbol == null && FullName != "Java.Lang.Object" && FullName != "System.Object") {
				Report.LogCodedWarning (0, Report.WarningUnknownBaseType, this, FullName, BaseType);
				IsValid = false;
				return false;
			}

			if ((base_symbol != null && !base_symbol.Validate (opt, TypeParameters, context)) || !base.OnValidate (opt, type_params, context)) {
				Report.LogCodedWarning (0, Report.WarningInvalidBaseType, this, FullName, BaseType);
				IsValid = false;
				return false;
			}

			var valid_ctors = new List<Ctor> ();

			foreach (var c in Ctors)
				if (c.Validate (opt, TypeParameters, context))
					valid_ctors.Add (c);

			Ctors = valid_ctors;

			return true;
		}

		public override void ResetValidation ()
		{
			validated = false;
			base.ResetValidation ();
		}

		public override string ToNative (CodeGenerationOptions opt, string varname, Dictionary<string, string> mappings = null) =>
			$"JNIEnv.ToLocalJniHandle ({varname})";

		public override void UpdateEnumsInInterfaceImplementation ()
		{
			foreach (InterfaceGen iface in GetAllDerivedInterfaces ()) {
				if (iface.HasEnumMappedMembers) {
					foreach (Method imethod in iface.Methods) {
						var method = Methods.FirstOrDefault (m => m.Name == imethod.Name && m.JniSignature == imethod.JniSignature);
						if (method != null) {
							if (imethod.IsReturnEnumified)
								method.RetVal.SetGeneratedEnumType (imethod.RetVal.FullName);
							for (int i = 0; i < imethod.Parameters.Count; i++)
								if (imethod.Parameters [i].IsEnumified)
									method.Parameters [i].SetGeneratedEnumType (imethod.Parameters [i].Type);
						}
					}
				}
			}
			base.UpdateEnumsInInterfaceImplementation ();
		}
	}
}
