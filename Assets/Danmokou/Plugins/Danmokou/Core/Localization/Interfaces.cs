namespace Danmokou.Core {

public interface ILangObject {
}

public interface ILangCountable : ILangObject {
    int Count { get; }
}


public class TrivialLangObject : ILangObject {
    public readonly object obj;

    public TrivialLangObject(object obj) {
        this.obj = obj;
    }

    public override string ToString() => obj.ToString();
}

public class NumberLangObject : TrivialLangObject, ILangCountable {
    public readonly int count;
    public NumberLangObject(int count) : base(count) {
        this.count = count;
    }

    public int Count => count;
}

public static class LocalizationInterfaceHelpers {
    public static T ResolveOneMany<T>(this ILangCountable ct, T singular, T many) =>
        ct.Count == 1 ? singular : many;
}

}