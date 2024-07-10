namespace YaGpt
{
    public class Const
    {
        public static MainConfig Config { get; private set; }

        public class MainConfig
        {
            public string db_conn { get; init; }
            public string ya_folder { get; init; }
            public string ya_auth { get; init; }
            public string[] allow_tokens { get; init; }
        }

        public static void Init()
        {
            var cfg = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json"));
            Config = cfg.ToTypedObject<MainConfig>();
            //SQL.InitDB();
        }
    }
}
