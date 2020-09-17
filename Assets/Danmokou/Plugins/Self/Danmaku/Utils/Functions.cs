using System;

namespace DMath {
public static class Functions {
    public static Action Link(Action a, Action b) => () => {
        a();
        b();
    };
}
}