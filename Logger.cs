class Logger {
    //for debugging
    public static void log(string s) {
        using (System.IO.StreamWriter file =
                    new System.IO.StreamWriter(@"kotb_log.txt", true)) {
            file.WriteLine(s);
        }
    }

    public static void log(GTA.Math.Vector3 v, string label) {
        string output = string.Format("{0}: X: {1} Y: {2} Z: {3}", label.ToUpper(), v.X, v.Y, v.Z);
        log(output);
    }
}
