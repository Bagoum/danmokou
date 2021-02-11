using System;
using NUnit.Framework;
using DMK.DMath;
using DMK.DMath.Functions;
using DMK.Expressions;
using DMK.Reflection;
using UnityEngine;
using static DMK.Reflection.CompilerHelpers;
using static DMK.Reflection.Compilers;

namespace DMK.Testing {

    public static class ReflectGeneric {

        [Test]
        public static void TGenericCompile() {
            var func = CompileDelegate<Func<float, float, float, Vector3>, Vector3>(
                "pxyz(&myVar1, &myVar2, &myVar3)",
                new DelegateArg<float>("myVar1"),
                new DelegateArg<float>("myVar2"),
                new DelegateArg<float>("myVar3")
            );
            TAssert.VecEq(func(4f, 7f, 8f), new Vector3(4f, 7f, 8f));
            Assert.Throws<Reflector.CompileException>(() => CompileDelegate<Func<float, float>, float>("&doesNotExist",
                new DelegateArg<float>("myVar1")));
            Assert.Throws<Reflector.BadTypeException>(() => CompileDelegate<Func<float, Vector2, float>, float>("&myVec",
                new DelegateArg<float>("myFloat"),
                new DelegateArg<Vector2>("myVec")));

        }
    }
}