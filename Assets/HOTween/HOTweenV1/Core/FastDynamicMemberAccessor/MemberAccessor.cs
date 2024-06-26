//
// FastDynamicMemberAccessor.cs
//
// Author: James Nies
// Licensed under The Code Project Open License (CPOL): http://www.codeproject.com/info/cpol10.aspx

#if !MICRO
using System;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace FastDynamicMemberAccessor
{
    internal sealed class ChainingAccessor : IMemberAccessor
    {
        readonly IMemberAccessor _pimp;
        readonly IMemberAccessor _chain;

        internal ChainingAccessor(IMemberAccessor impl, IMemberAccessor chain)
        {
            _pimp = impl;
            _chain = chain;
        }

        public object Get(object target)
        {
            return _pimp.Get(_chain.Get(target));
        }

        public void Set(object target, object value)
        {
            _pimp.Set(_chain.Get(target), value);
        }
    }

    internal abstract class MemberAccessor : IMemberAccessor
    {
        const string emmitedTypeName = "Member";

        /// <summary>
        /// Creates a new member accessor.
        /// </summary>
        /// <param name="member">Member</param>
        protected MemberAccessor(MemberInfo member)
        {
            _targetType = member.ReflectedType;
            _fieldName = member.Name;
        }

//        internal static MemberAccessor Make(Type targetType, string member)
//        {
//            //
//            // Make sure the member exists
//            //
//            MemberInfo[] matches = targetType.GetMember(member,
//                MemberTypes.Field | MemberTypes.Property,
//                BindingFlags.Public | BindingFlags.Instance);
//
//            if((matches == null) || (matches.Length==0))
//                throw new MemberAccessorException(string.Format("Member \"{0}\" does not exist for type {1}.", member, targetType));
//
//            return Make(matches[0]);
//        }
//
//        private static readonly IDictionary _cache = new Hashtable();
//        internal static MemberAccessor Make(MemberInfo member)
//        {
//            if (!_cache.Contains(member))
//            {
//                switch (member.MemberType)
//                {
//                    case MemberTypes.Field:
//                        _cache.Add(member, new FieldAccessor((FieldInfo) member));
//                        break;
//                    case MemberTypes.Property:
//                        _cache.Add(member, new PropertyAccessor((PropertyInfo) member));
//                        break;
//                    default:
//                        throw new InvalidProgramException("Unreachable code executed");
//                }
//            }
//
//            return (MemberAccessor) _cache[member];
//
//            // DG MOD - remove cache (my MemberAccessorCacher is more efficient)
//
//            switch ( member.MemberType ) {
//            case MemberTypes.Field:
//                return new FieldAccessor((FieldInfo) member);
//            case MemberTypes.Property:
//                return new PropertyAccessor((PropertyInfo) member);
//            default:
//                throw new InvalidProgramException("Unreachable code executed");
//            }
//        }

        /// <summary>
        /// Added by Daniele Giardini for HOTween,
        /// because if a Make is called we already know that a PropertyInfo or FieldInfo exist,
        /// and we can directly pass them as parameters.
        /// </summary>
        internal static MemberAccessor Make(PropertyInfo p_propertyInfo, FieldInfo p_fieldInfo)
        {
            if (p_propertyInfo != null)
            {
                return new PropertyAccessor(p_propertyInfo);
            }

            return new FieldAccessor(p_fieldInfo);
        }

        /// <summary>
        /// Thanks to Ben Ratzlaff for this snippet of code
        /// http://www.codeproject.com/cs/miscctrl/CustomPropGrid.asp
        ///
        /// "Initialize a private hashtable with type-opCode pairs
        /// so i dont have to write a long if/else statement when outputting msil"
        /// </summary>
        static MemberAccessor()
        {
            s_TypeHash = new Hashtable();
            s_TypeHash[typeof(sbyte)] = OpCodes.Ldind_I1;
            s_TypeHash[typeof(byte)] = OpCodes.Ldind_U1;
            s_TypeHash[typeof(char)] = OpCodes.Ldind_U2;
            s_TypeHash[typeof(short)] = OpCodes.Ldind_I2;
            s_TypeHash[typeof(ushort)] = OpCodes.Ldind_U2;
            s_TypeHash[typeof(int)] = OpCodes.Ldind_I4;
            s_TypeHash[typeof(uint)] = OpCodes.Ldind_U4;
            s_TypeHash[typeof(long)] = OpCodes.Ldind_I8;
            s_TypeHash[typeof(ulong)] = OpCodes.Ldind_I8;
            s_TypeHash[typeof(bool)] = OpCodes.Ldind_I1;
            s_TypeHash[typeof(double)] = OpCodes.Ldind_R8;
            s_TypeHash[typeof(float)] = OpCodes.Ldind_R4;
        }

        /// <summary>
        /// Gets the member value from the specified target.
        /// </summary>
        /// <param name="target">Target object.</param>
        /// <returns>Member value.</returns>
        public object Get(object target)
        {
            if (CanRead)
            {
                EnsureInit();

                return _emittedMemberAccessor.Get(target);
            }
            else
            {
                throw new MemberAccessorException(
                    string.Format("Member \"{0}\" does not have a get method.",
                                  _fieldName));
            }
        }

        /// <summary>
        /// Sets the member for the specified target.
        /// </summary>
        /// <param name="target">Target object.</param>
        /// <param name="value">Value to set.</param>
        public void Set(object target, object value)
        {
            if (CanWrite)
            {
                EnsureInit();

                //
                // Set the member value
                //
                _emittedMemberAccessor.Set(target, value);
            }
            else
            {
                throw new MemberAccessorException(
                    string.Format("Member \"{0}\" does not have a set method.",
                                  _fieldName));
            }
        }

        /// <summary>
        /// Whether or not the Member supports read access.
        /// </summary>
        internal abstract bool CanRead { get; }

        /// <summary>
        /// Whether or not the Member supports write access.
        /// </summary>
        internal abstract bool CanWrite { get; }

        /// <summary>
        /// The Type of object this member accessor was
        /// created for.
        /// </summary>
        internal Type TargetType
        {
            get
            {
                return _targetType;
            }
        }

        /// <summary>
        /// The Type of the Member being accessed.
        /// </summary>
        internal abstract Type MemberType { get; }

        //
        protected readonly Type _targetType;
        protected readonly string _fieldName;
        protected static readonly Hashtable s_TypeHash;
        //
        IMemberAccessor _emittedMemberAccessor;

        /// <summary>
        /// This method generates creates a new assembly containing
        /// the Type that will provide dynamic access.
        /// </summary>
        void EnsureInit()
        {
            if (_emittedMemberAccessor == null)
            {
                // Create the assembly and an instance of the
                // member accessor class.
                //Assembly assembly = EmitAssembly();

                //_emittedMemberAccessor = assembly.CreateInstance(emmitedTypeName) as IMemberAccessor;

                if (_emittedMemberAccessor == null)
                {
                    throw new Exception("Unable to create member accessor.");
                }
            }
        }

        /// <summary>
        /// Create an assembly that will provide the get and set methods.
        /// </summary>
        //Assembly EmitAssembly()
        //{
        //    //
        //    // Create an assembly name
        //    //
        //    AssemblyName assemblyName = new AssemblyName();
        //    assemblyName.Name = "PropertyAccessorAssembly";

        //    //
        //    // Create a new assembly with one module
        //    //
        //    //AssemblyBuilder newAssembly = Thread.GetDomain().DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        //    //ModuleBuilder newModule = newAssembly.DefineDynamicModule("Module");

        //    //
        //    //  Define a public class named emmitedTypeName in the assembly.
        //    //
        //    //TypeBuilder myType =
        //    //    newModule.DefineType(emmitedTypeName, TypeAttributes.Public | TypeAttributes.Sealed);

        //    //
        //    // Mark the class as implementing IMemberAccessor.
        //    //
        //    //myType.AddInterfaceImplementation(typeof(IMemberAccessor));

        //    //// Add a constructor
        //    ///*ConstructorBuilder constructor = */
        //    //myType.DefineDefaultConstructor(MethodAttributes.Public);

        //    //_EmitGetter(myType);
        //    //_EmitSetter(myType);

        //    ////
        //    //// Load the type
        //    ////
        //    //myType.CreateType();

        //    //return newAssembly;
        //}

        protected abstract void _EmitGetter(TypeBuilder type);
        protected abstract void _EmitSetter(TypeBuilder type);
    }
}
#endif
