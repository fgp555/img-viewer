if (args.Length == 3 && args[0] == "--parent")
{
    File.WriteAllText(args[1], "ready");
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    while (!File.Exists(args[2])) await Task.Delay(100, timeout.Token);
}
else File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "restarted.txt"), "Restarted " + typeof(Program).Assembly.GetName().Version);
