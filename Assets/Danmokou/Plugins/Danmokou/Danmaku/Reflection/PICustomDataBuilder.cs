using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Reflection;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.Danmaku.Descriptors;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.Player;
using UnityEngine;
using Ex = System.Linq.Expressions.Expression;

namespace Danmokou.Reflection.CustomData {
public readonly struct TypeDefKey {
    public readonly List<IReadOnlyList<(Reflector.ExType type, string name)>> Exposed;
    public TypeDefKey(List<IReadOnlyList<(Reflector.ExType, string)>> exposed) {
        this.Exposed = exposed;
    }

    //Call this when using as a persistent key so elements don't get modified later
    public TypeDefKey Freeze() => 
        new(Exposed.Select(s => s.ToList() as IReadOnlyList<(Reflector.ExType, string)>).ToList());

    public override bool Equals(object obj) =>
        obj is TypeDefKey td && Exposed.AreSameNested(td.Exposed);

    public override int GetHashCode() {
        var result = 17;
        foreach (var ele in Exposed)
            result = result * 23 + ele.ElementWiseHashCode();
        return result;
    }

    public static readonly TypeDefKey Empty = new(new());
}

public class ConstructedType {
    public BuiltCustomDataDescriptor Descriptor { get; }
    public Type BuiltType { get; }
    public Func<PICustomData> Constructor { get; }
    public Stack<PICustomData> Cache { get; } = new();
    public int TypeIndex { get; }
    public int Allocated { get; private set; } 
    public int Popped { get; private set; } //Popped and recached should be about equal
    public int Recached { get; private set; }
    public int Copied { get; internal set; }
    public int Cleared { get; private set; }
    
    public ConstructedType(BuiltCustomDataDescriptor desc, Type builtType, int typeIndex) {
        this.Descriptor = desc;
        this.BuiltType = builtType;
        this.Constructor = Ex.Lambda<Func<PICustomData>>(Ex.New(builtType.GetConstructor(Type.EmptyTypes)!)).Compile();
        this.TypeIndex = typeIndex;
    }

    public PICustomData MakeNew(GenCtx? gcx = null) {
        PICustomData data;
        if (Cache.Count > 0) {
            data = Cache.Pop();
            ++Popped;
        } else {
            data = Constructor();
            ++Allocated;
        }
        data.typeIndex = TypeIndex;
        data.firer = gcx?.exec;
        data.playerController = data.firer switch {
            PlayerController pi => pi,
            FireOption fo => fo.Player,
            Bullet b => b.Player?.firer,
            _ => null
        };
        if (data.playerController == null)
            data.playerController = gcx?.playerController;
        return data;
    }

    public void Return(PICustomData data) {
        data.firer = null;
        data.playerController = null;
        data.laserController = null;
        data.playerBullet = null;
        ++Recached;
        Cache.Push(data);
    }

}

public class PICustomDataBuilder : CustomDataBuilder {
    private readonly Dictionary<TypeDefKey, ConstructedType> typeMap = new();
    private readonly List<ConstructedType> typeList = new();
    
    public PICustomDataBuilder() : base(
        typeof(PICustomData), "DanmokouDynamic", null, typeof(float), typeof(int), typeof(Vector2), typeof(Vector3), typeof(V2RV2)) {
        var consType = new ConstructedType(customDataDescriptors[CustomDataBaseType], CustomDataBaseType, 0);
        typeList.Add(consType);
        typeMap[TypeDefKey.Empty] = consType;
    }

    public ConstructedType GetTypeDef(PICustomData pi) => typeList[pi.typeIndex];

    public override void SetReservedKeys() {
        variableNameToID[(PICustomData.FLIPX, ExUtils.tfloat)] = PICustomData.FLIPX_KEY;
        variableNameToID[(PICustomData.FLIPY, ExUtils.tfloat)] = PICustomData.FLIPY_KEY;
    }
    
    PICustomData GetCustomData(GenCtx gcx, List<IReadOnlyList<(Reflector.ExType, string)>> aliases, ref ConstructedType? type) {
        type ??= GetCustomDataType(new TypeDefKey(aliases));
        return type.MakeNew(gcx);
    }

    PICustomData GetCustomData(GenCtx gcx, IReadOnlyList<(Reflector.ExType, string)> aliases, ref ConstructedType? type) {
        if (type != null)
            return type.MakeNew(gcx);
        var lis = ListCache<IReadOnlyList<(Reflector.ExType, string)>>.Get();
        lis.Add(aliases);
        var result = GetCustomData(gcx, lis, ref type);
        ListCache<IReadOnlyList<(Reflector.ExType, string)>>.Consign(lis);
        return result;
    }
    public ConstructedType GetCustomDataType(in TypeDefKey key) {
        if (typeMap.TryGetValue(key, out var t))
            return t;
        var builtType = Builder.CreateCustomDataType(new(
            key.Exposed.SelectMany(lis => lis.Select(x =>
                new CustomDataFieldDescriptor(x.name, x.type.AsType())
            )).ToArray()
        ) { BaseType = typeof(PICustomData) }, out var builtDesc);
        t = new(builtDesc, builtType, typeList.Count);
        Logs.Log($"Created custom data type with fields {builtDesc.Descriptor}");
        typeList.Add(t);
        return typeMap[key.Freeze()] = t;
    }

    public ConstructedType GetCustomDataType(IReadOnlyList<(Reflector.ExType, string)> aliases) {
        var lis = ListCache<IReadOnlyList<(Reflector.ExType, string)>>.Get();
        lis.Add(aliases);
        var result = GetCustomDataType(new TypeDefKey(lis));
        ListCache<IReadOnlyList<(Reflector.ExType, string)>>.Consign(lis);
        return result;
    }


    public static readonly PICustomDataBuilder Builder = new();
}
}