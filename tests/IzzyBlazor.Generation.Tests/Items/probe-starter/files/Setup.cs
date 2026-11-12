namespace {{RootNamespace}};

public static class Setup
{
#if (UseServer)
    public const string Mode = "interactive";
#else
    public const string Mode = "static";
#endif
}
