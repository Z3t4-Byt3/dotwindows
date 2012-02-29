// build this with "csc /r:ZetaLongPaths.dll /t:exe /out:myke.exe /debug+ myke*.cs"

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

public class App {
  public static int Main(String[] args) {
    var path = @"Software\Far2\KeyMacros\Vars";
    var reg = Registry.CurrentUser.OpenSubKey(path, true) ?? Registry.CurrentUser.CreateSubKey(path);
    var env_values = reg.GetValueNames().Where(name => name.StartsWith("%%Myke")).ToList();
    env_values.ForEach(value => reg.DeleteValue(value));

    try {
      try {
        var status = MainWrapper(args);
        Environment.ExitCode = status.value;
      } catch {
        Environment.ExitCode = -1;
        throw;
      }
    } finally {
      Registry.SetValue(@"HKEY_CURRENT_USER\Software\Far2\KeyMacros\Vars", "%%MykeStatus", Environment.ExitCode.ToString());
      Func<String, String> capitalize = s => String.IsNullOrEmpty(s) ? s : Char.ToUpper(s[0]) + s.Substring(1);
      var env = (AppDomain.CurrentDomain.GetData("%%MykeEnv") as Dictionary<String, String>) ?? new Dictionary<String, String>();
      env.Keys.ToList().ForEach(key => Registry.SetValue(@"HKEY_CURRENT_USER\Software\Far2\KeyMacros\Vars", "%%Myke" + capitalize(key), env[key]));
    }

    return Environment.ExitCode;
  }

  private static ExitCode MainWrapper(String[] args) {
    var status = Config.parse(args);
    if (status != 0) return status;

    if (Config.verbose) {
      Config.print();
    }

    var originalTarget = Config.target;
    var t_all = Connectors.all;
    if (Config.verbose) {
      Console.println();
      Console.println("connectors: ");
      Help.printConnectors();
    }

    while (Config.target != null) {
      if (Config.verbose) {
        Console.println();
        Console.println("considering target {0}", Config.target);
      }

      foreach (var t_conn in t_all) {
        if (Config.verbose) {
          Console.print("* {0}... ", t_conn.name());
        }

        var conn = t_conn.instantiate();
        var accepts = conn != null && conn.accept();
        if (Config.verbose) {
          if (accepts) Console.println("[A]");
          else Console.println("[R]");
        }

        if (accepts) {
          var action = Config.action;
          if (Config.verbose) {
            Console.println();
            Console.println("========================================");
            Console.println("{0} will now perform {1} upon {2}", conn.name(), Config.action, Config.target);
            Console.println("========================================");
            Console.println();
          }

          var actions = conn.actions();
          if (!actions.ContainsKey(action)) {
            Console.println("error: {0} does not know how to do {1}", conn.name(), Config.action);
            return -1;
          }

          ExitCode exitCode = -1;
          try {
            exitCode = (ExitCode)actions[action]();
            return exitCode;
          } finally {
            var env = (AppDomain.CurrentDomain.GetData("%%MykeEnv") as Dictionary<String, String>) ?? new Dictionary<String, String>();
            if (!env.ContainsKey("action")) env["action"] = action;
            if (!env.ContainsKey("target")) env["target"] = Config.target;
            if (!env.ContainsKey("args")) env["args"] = Config.args.ToString();
            if (!env.ContainsKey("meaningful")) {
              env["meaningful"] = (exitCode ? 0 : 1).ToString();
              if (action == "run" || action == "repl" || action == "console") env["meaningful"] = "1";
            }
          }
        }
      }

      Config.target = Path.GetDirectoryName(Config.target);
    }

    if (Config.verbose) {
      Console.println();
    }

    Console.println("error: no connector accepted the target {0}", originalTarget);
    return -1;
  }
}

public class ExitCode {
  public int value;

  public static ExitCode operator !(ExitCode c) {
    return c.value == 0 ? new ExitCode{value = -1} : new ExitCode{value = 0};
  }

  public static bool operator ==(ExitCode c1, ExitCode c2) {
    return Equals(c1, c2);
  }

  public static bool operator !=(ExitCode c1, ExitCode c2) {
    return !Equals(c1, c2);
  }

  public override bool Equals(Object o_other) {
    var other = o_other as ExitCode;
    if (other == null) return false;
    return this.value == other.value;
  }

  public override int GetHashCode() {
    return value;
  }

  public override string ToString() {
    return value.ToString();
  }

  public static implicit operator ExitCode(bool value) {
    return new ExitCode { value = value ? 0 : -1 };
  }

  public static implicit operator ExitCode(int value) {
    return new ExitCode { value = value };
  }

  public static ExitCode operator &(ExitCode c1, ExitCode c2) {
    return c1 ? c2 : 0;
  }

  public static ExitCode operator |(ExitCode c1, ExitCode c2) {
    return c1 ? 0 : c2;
  }

  public static bool operator true(ExitCode c) {
    return c.value == 0;
  }

  public static bool operator false(ExitCode c) {
    return c.value != 0;
  }

  public static implicit operator bool(ExitCode c) {
    return c.value == 0;
  }
}

public static class Console {
  public static void print(String format, params Object[] objs) {
    System.Console.Write(String.Format(format, objs));
  }

  public static void print(String obj) {
    System.Console.Write(obj);
  }

  public static void print(Object obj) {
    System.Console.Write(obj);
  }

  public static void println(String format, params Object[] objs) {
    System.Console.WriteLine(String.Format(format, objs));
  }

  public static void println(String obj) {
    System.Console.WriteLine(obj);
  }

  public static void println(Object obj) {
    System.Console.WriteLine(obj);
  }

  public static void println() {
    System.Console.WriteLine();
  }

  public class Point {
    public int x;
    public int y;

    public Point(int x, int y) {
      this.x = x;
      this.y = y;
    }

    public Point(Point other) {
      this.x = other.x;
      this.y = other.y;
    }

    public override String ToString() {
      return String.Format("x = {0}, y = {0}", x, y);
    }
  }

  public static String readln(String prompt = null, String history = null) {
    if (history != null) {
      var key = @"Software\Myke\" + history.Replace("\\", "$slash$");
      var reg = Registry.CurrentUser.OpenSubKey(key, true) ?? Registry.CurrentUser.CreateSubKey(key);

      var indices = reg.GetValueNames().Where(name => name.StartsWith("arguments")).Select(name => {
        int index;
        return int.TryParse(name.Substring("arguments".Length), out index) ? index: -1;
      }).OrderBy(index => index).Where(index => index != -1).ToList();
      var maxIndex = indices.Count == 0 ? 0 : indices.Max();
      var fullList = indices.Select(index => reg.GetValue("arguments" + index).ToString()).ToList();
      var shortList = indices.Select(index => reg.GetValue("arguments" + index).ToString()).Distinct().ToList();

      var @default = fullList.LastOrDefault() ?? String.Empty;
      if (!String.IsNullOrEmpty(prompt)) {
        print(prompt);
        if (!String.IsNullOrEmpty(@default)) print(" (default is {0})", @default);
        print(": ");
      };

      var input = ((Func<String>)(() => {
        var dumb = ((Func<bool>)(() => { try { var aux = System.Console.KeyAvailable; return false; } catch { return true; } }))();
        if (dumb) {
          return System.Console.ReadLine();
        } else {
          var logfile = ((Func<String>)(() => {
            if (File.Exists(Config.originalTarget)) return Config.originalTarget + ".log";
            if (Directory.Exists(Config.originalTarget)) return Config.originalTarget + ".log";
            return null;
          }))();

          if (logfile != null && File.Exists(logfile)) File.Delete(logfile);
          Action<String> log = msg => {
            if (Config.verbose && logfile != null) {
              File.AppendAllText(logfile, msg + "\r\n");
            }
          };

          var buf = String.Empty;
          var oldbuf = buf;
          var historypos = shortList.Count;
          Action dechistorypos = () => { if (historypos > 0) historypos--; };
          Action inchistorypos = () => { if (historypos < shortList.Count) historypos++; };
          Func<String> currenthistory = () => historypos == shortList.Count ? "" : shortList[historypos];
          var pos = new Point(System.Console.CursorLeft, System.Console.CursorTop);
          var origpos = new Point(System.Console.CursorLeft, System.Console.CursorTop);
          log(String.Format("original pos: x = {0}, y = {1}", origpos.x, origpos.y));
          Func<int> currentpos = () => (pos.y - origpos.y) * System.Console.WindowWidth + (pos.x - origpos.x);
          Func<Char> currentchar = () => currentpos() >= buf.Length ? '\0' : buf[currentpos()];
          Action<int> gotopos = null;
          gotopos = idx => {
            if (idx < 0) gotopos(0);
            else if (idx > buf.Length) gotopos(buf.Length);
            else {
              log(String.Format("going to {0}", idx));
              log(String.Format("currentpos: {0}", currentpos()));
              log(String.Format("pos is now {0}", pos));
              pos = new Point(origpos);
              pos.x += idx;
              while (pos.x > System.Console.WindowWidth) {
                pos.x -= System.Console.WindowWidth;
                pos.y += 1;
              }
              log(String.Format("currentpos: {0}", currentpos()));
              log(String.Format("pos is now {0}", pos));
            }
          };
          Action decpos = () => gotopos(currentpos() - 1);
          Action incpos = () => gotopos(currentpos() + 1);
          Action redraw = () => {
            System.Console.SetCursorPosition(origpos.x, origpos.y);
            System.Console.Write(new String(' ', oldbuf.Length));
            System.Console.SetCursorPosition(origpos.x, origpos.y);
            System.Console.Write(buf);
            oldbuf = buf;
            System.Console.SetCursorPosition(pos.x, pos.y);
          };

          while (true) {
            log("");
            log("===readkey===");
            log(String.Format("buf = {0}, currentpos = {1}", buf, currentpos()));
            log(String.Format("[before] pos: x = {0}, y = {1}", System.Console.CursorLeft, System.Console.CursorTop));
            var kp = System.Console.ReadKey();
            log(String.Format("key = {0}, char = {1}, modifiers = {2}", kp.Key, ((int)kp.KeyChar).ToString("x4"), kp.Modifiers));
            log(String.Format("[after] pos: x = {0}, y = {1}", System.Console.CursorLeft, System.Console.CursorTop));

            if (kp.KeyChar != '\0') {
              if (kp.KeyChar == 0x0d) {
                println();
                break;
              } else if (kp.KeyChar == 0x08) { // backspace
                if (currentpos() > 0) {
                  var buf1 = new StringBuilder();
                  if (currentpos() > 0 && buf.Length > 1) buf1.Append(buf.Substring(0, currentpos() - 1));
                  if (currentpos() < buf.Length) buf1.Append(buf.Substring(currentpos(), buf.Length - currentpos()));
                  buf = buf1.ToString();
                  decpos();
                }
              } else if (kp.KeyChar == 0x1b) { // escape
                gotopos(0);
                buf = "";
              } else if (kp.KeyChar < 0x20) {
                // do nothing
              } else {
                var buf1 = new StringBuilder();
                if (currentpos() > 0) buf1.Append(buf.Substring(0, currentpos()));
                buf1.Append(kp.KeyChar);
                if (currentpos() < buf.Length) buf1.Append(buf.Substring(currentpos(), buf.Length - currentpos()));
                buf = buf1.ToString();
                gotopos(currentpos() + 1);
              }
            } else {
              if (kp.Key == ConsoleKey.Home) {
                gotopos(0);
              } else if (kp.Key == ConsoleKey.End) {
                gotopos(buf.Length);
              } if (kp.Key == ConsoleKey.LeftArrow && !kp.Modifiers.HasFlag(ConsoleModifiers.Control)) {
                decpos();
              } if (kp.Key == ConsoleKey.RightArrow && !kp.Modifiers.HasFlag(ConsoleModifiers.Control)) {
                incpos();
              } if (kp.Key == ConsoleKey.LeftArrow && kp.Modifiers.HasFlag(ConsoleModifiers.Control)) {
                decpos();
                if (Char.IsLetterOrDigit(currentchar())) {
                  while (currentpos() != 0 && Char.IsLetterOrDigit(currentchar())) decpos();
                  if (!Char.IsLetterOrDigit(currentchar())) incpos();
                } else {
                  while (currentpos() != 0 && !Char.IsLetterOrDigit(currentchar())) decpos();
                  if (Char.IsLetterOrDigit(currentchar())) {
                    while (currentpos() != 0 && Char.IsLetterOrDigit(currentchar())) decpos();
                    if (!Char.IsLetterOrDigit(currentchar())) incpos();
                  }
                }
              } if (kp.Key == ConsoleKey.RightArrow && kp.Modifiers.HasFlag(ConsoleModifiers.Control)) {
                incpos();
                if (Char.IsLetterOrDigit(currentchar())) {
                  while (currentpos() != 0 && Char.IsLetterOrDigit(currentchar())) incpos();
                  if (!Char.IsLetterOrDigit(currentchar())) {
                    while (currentpos() != 0 && !Char.IsLetterOrDigit(currentchar())) incpos();
                  }
                } else {
                  while (currentpos() != 0 && !Char.IsLetterOrDigit(currentchar())) incpos();
                }
              } else if (kp.Key == ConsoleKey.UpArrow) {
                if (historypos > 0) {
                  gotopos(0);
                  dechistorypos();
                  buf = currenthistory();
                  gotopos(buf.Length);
                }
              } else if (kp.Key == ConsoleKey.DownArrow) {
                if (historypos < shortList.Count) {
                  gotopos(0);
                  inchistorypos();
                  buf = currenthistory();
                  gotopos(buf.Length);
                }
              } else if (kp.Key == ConsoleKey.Insert || (kp.Key == ConsoleKey.V && kp.Modifiers.HasFlag(ConsoleModifiers.Control))) {
                var text = Clipboard.GetText();
                var buf1 = new StringBuilder();
                if (currentpos() > 0) buf1.Append(buf.Substring(0, currentpos()));
                buf1.Append(text);
                if (currentpos() < buf.Length) buf1.Append(buf.Substring(currentpos(), buf.Length - currentpos()));
                buf = buf1.ToString();
                gotopos(currentpos() + text.Length);
              } else if (kp.Key == ConsoleKey.Escape) {
                gotopos(0);
                buf = "";
              } else if (kp.Key == ConsoleKey.Delete) {
                var buf1 = new StringBuilder();
                if (currentpos() > 0) buf1.Append(buf.Substring(0, currentpos()));
                if (currentpos() < buf.Length && buf.Length > 1) buf1.Append(buf.Substring(currentpos() + 1, buf.Length - currentpos() - 1));
                buf = buf1.ToString();
              } else if (kp.Key == ConsoleKey.Backspace) {
                if (currentpos() > 0) {
                  var buf1 = new StringBuilder();
                  if (currentpos() > 0 && buf.Length > 1) buf1.Append(buf.Substring(0, currentpos() - 1));
                  if (currentpos() < buf.Length) buf1.Append(buf.Substring(currentpos(), buf.Length - currentpos()));
                  buf = buf1.ToString();
                  decpos();
                }
              }
            }

            log(String.Format("[result] buf = {0}, currentpos = {1}", buf, currentpos()));
            redraw();
            log(String.Format("[finally] pos: x = {0}, y = {1}", System.Console.CursorLeft, System.Console.CursorTop));
          }

          return buf;
        }
      }))();

      if (input == String.Empty && !String.IsNullOrEmpty(prompt)) input = @default;
      if (input == "<empty>" && !String.IsNullOrEmpty(prompt)) input = String.Empty;

      if (Config.verbose) {
        println("all right, input is: {0}", input);
        println();
      }

      reg.SetValue("arguments" + (maxIndex + 1), input, RegistryValueKind.String);
      reg.SetValue("lastarguments", input, RegistryValueKind.String);
      return input;
    } else {
      if (prompt != null && prompt != String.Empty) print(prompt + ": ");
      return System.Console.ReadLine();
    }
  }

  private static List<String> internalEval(String command, DirectoryInfo home = null) {
    var script = Path.GetTempFileName() + ".bat";
    File.AppendAllText(script, "@echo off" + "\r\n"); // always @echo off, so that eval works correctly
    File.AppendAllText(script, "cd /D \"" + (home ?? new DirectoryInfo(".")).FullName + "\"" + "\r\n");
    File.AppendAllText(script, command + "\r\n");
    File.AppendAllText(script, "exit /b %errorlevel%");

    var psi = new ProcessStartInfo();
    psi.FileName = script;
    psi.WorkingDirectory = (home ?? new DirectoryInfo(".")).FullName;
    psi.UseShellExecute = false;
    psi.RedirectStandardOutput = true;
    psi.RedirectStandardError = true;

    if (Config.verbose) {
      Console.println("psi: filename = {0}, arguments = {1}, home = {2}", psi.FileName, psi.Arguments, psi.WorkingDirectory);
      Console.println("script at {0} is:", script);
      Console.println(File.ReadAllText(script));
      Console.println();
      Console.println("========================================");
      Console.println("The command will now be executed by cmd.exe");
      Console.println("========================================");
      Console.println();
    }

    if (Config.dryrun) {
      println("eval: {0} {1} at {2}", psi.FileName, psi.Arguments, psi.WorkingDirectory);
      return null;
    } else {
      var p = new Process();
      p.StartInfo = psi;

      var buf = new StringBuilder();
      p.OutputDataReceived += (sender, args) => { if (args.Data != null) buf.Append(args.Data + "\r\n"); };
      p.ErrorDataReceived += (sender, args) => { if (args.Data != null) buf.Append(args.Data + "\r\n  "); };

      if (p.Start()) {
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        p.WaitForExit();
        return p.ExitCode == 0 ? buf.ToString().Split(new []{"\r\n"}, StringSplitOptions.None).ToList() : null;
      } else {
        return null;
      }
    }
  }

  private static ExitCode cmd(String command, DirectoryInfo home = null) {
    var script = Path.GetTempFileName() + ".bat";
    if (!Config.verbose) File.AppendAllText(script, "@echo off" + "\r\n");
    File.AppendAllText(script, "cd /D \"" + (home ?? new DirectoryInfo(".")).FullName + "\"" + "\r\n");
    File.AppendAllText(script, command + "\r\n");
    File.AppendAllText(script, "exit /b %errorlevel%");

    var psi = new ProcessStartInfo();
    psi.FileName = script;
    psi.WorkingDirectory = (home ?? new DirectoryInfo(".")).FullName;
    psi.UseShellExecute = false;

    if (Config.verbose) {
      Console.println("psi: filename = {0}, arguments = {1}, home = {2}", psi.FileName, psi.Arguments, psi.WorkingDirectory);
      Console.println("script at {0} is:", script);
      Console.println(File.ReadAllText(script));
      Console.println();
      Console.println("========================================");
      Console.println("The command will now be executed by cmd.exe");
      Console.println("========================================");
      Console.println();
    }

    if (Config.dryrun) {
      println("exec: {0} {1} at {2}", psi.FileName, psi.Arguments, psi.WorkingDirectory);
      return -1;
    } else {
      var p = new Process();
      p.StartInfo = psi;

      if (p.Start()) {
        p.WaitForExit();
        return p.ExitCode;
      } else {
        return -1;
      }
    }
  }

  private static ExitCode shellex(String command, DirectoryInfo home = null) {
    command = command.TrimStart(new []{' '});

    var i = 0;
    var buf = new StringBuilder();
    var in_quote = false;
    Func<bool> prev_slash = () => i == 1 ? false : command[i - 2] == '\\';
    while (i < command.Length) {
      var c = command[i++];

      if (c == '\"') {
        if (prev_slash()) {
          buf.Remove(buf.Length - 1, 1);
          buf.Append(c);
        } else {
          if (in_quote) {
            in_quote = false;
            break;
          } else {
            in_quote = true;
          }
        }
      } else if (c == '\\') {
        if (prev_slash()) {
          buf.Remove(buf.Length - 1, 1);
          buf.Append(c);
        } else {
          buf.Append(c);
        }
      } else if (c == ' ') {
        if (prev_slash()) {
          buf.Remove(buf.Length - 1, 1);
          buf.Append(c);
        } else {
          if (in_quote) {
            buf.Append(c);
          } else {
            break;
          }
        }
      } else {
        buf.Append(c);
      }
    }

    var psi = new ProcessStartInfo();
    psi.FileName = buf.ToString();
    psi.Arguments = command.Substring(i);
    psi.WorkingDirectory = (home ?? new DirectoryInfo(".")).FullName;
    psi.UseShellExecute = true;

    if (Config.verbose) {
      Console.println("psi: filename = {0}, arguments = {1}, home = {2}", psi.FileName, psi.Arguments, psi.WorkingDirectory);
      Console.println();
      Console.println("========================================");
      Console.println("The command will now be executed by shellex");
      Console.println("========================================");
      Console.println();
    }

    if (Config.dryrun) {
      println("shellexec: {0} {1} at {2}", psi.FileName, psi.Arguments, psi.WorkingDirectory);
      return -1;
    } else {
      var p = new Process();
      p.StartInfo = psi;

      if (p.Start()) {
        return 0;
      } else {
        return -1;
      }
    }
  }

  public static List<String> eval(String command) {
    return eval(command, null as DirectoryInfo);
  }

  public static List<String> eval(String command, String home = null) {
    return eval(command, home == null ? null as DirectoryInfo : new DirectoryInfo(home));
  }

  public static List<String> eval(String command, DirectoryInfo home = null) {
    if (home == null) {
      home = new DirectoryInfo(".");
      if (Directory.Exists(Config.target)) home = new DirectoryInfo(Config.target);
      if (File.Exists(Config.target)) home = new FileInfo(Config.target).Directory;
    }

    var env = (AppDomain.CurrentDomain.GetData("%%MykeEnv") as Dictionary<String, String>) ?? new Dictionary<String, String>();
    env["workingDir"] = home.FullName;
    return internalEval(command, home);
  }

  public static ExitCode batch(String command) {
    return batch(command, null as DirectoryInfo);
  }

  public static ExitCode batch(String command, String home = null) {
    return batch(command, home == null ? null as DirectoryInfo : new DirectoryInfo(home));
  }

  public static ExitCode batch(String command, DirectoryInfo home = null) {
    if (home == null) {
      home = new DirectoryInfo(".");
      if (Directory.Exists(Config.target)) home = new DirectoryInfo(Config.target);
      if (File.Exists(Config.target)) home = new FileInfo(Config.target).Directory;
    }

    var env = (AppDomain.CurrentDomain.GetData("%%MykeEnv") as Dictionary<String, String>) ?? new Dictionary<String, String>();
    env["workingDir"] = home.FullName;
    return cmd(command, home);
  }

  public static ExitCode interactive(String command) {
    return interactive(command, null as DirectoryInfo);
  }

  public static ExitCode interactive(String command, String home = null) {
    return interactive(command, home == null ? null as DirectoryInfo : new DirectoryInfo(home));
  }

  public static ExitCode interactive(String command, DirectoryInfo home = null) {
    if (home == null) {
      home = new DirectoryInfo(".");
      if (Directory.Exists(Config.target)) home = new DirectoryInfo(Config.target);
      if (File.Exists(Config.target)) home = new FileInfo(Config.target).Directory;
    }

    var env = (AppDomain.CurrentDomain.GetData("%%MykeEnv") as Dictionary<String, String>) ?? new Dictionary<String, String>();
    env["workingDir"] = home.FullName;
    return cmd(command, home);
  }

  public static ExitCode ui(String command) {
    return ui(command, null as DirectoryInfo);
  }

  public static ExitCode ui(String command, String home = null) {
    return ui(command, home == null ? null as DirectoryInfo : new DirectoryInfo(home));
  }

  public static ExitCode ui(String command, DirectoryInfo home = null) {
    if (home == null) {
      home = new DirectoryInfo(".");
      if (Directory.Exists(Config.target)) home = new DirectoryInfo(Config.target);
      if (File.Exists(Config.target)) home = new FileInfo(Config.target).Directory;
    }

    var env = (AppDomain.CurrentDomain.GetData("%%MykeEnv") as Dictionary<String, String>) ?? new Dictionary<String, String>();
    env["workingDir"] = home.FullName;
    return shellex(command, home);
  }
}

public static class Env {
  public static String Expand(this String s) {
    return new Regex("%(?<envvar>.*?)%").Replace(s, m => Environment.GetEnvironmentVariable(m.Result("${envvar}")));
  }
}

public static class Shell {
  public static String ShellEscape(this String s) {
    return s.Contains(" ") ? "\"" + s + "\"" : s;
  }
}

public static class FileSystem {
  public static bool IsEquivalentTo(this FileSystemInfo fsi1, FileSystemInfo fsi2) {
    if (fsi1 == null || fsi2 == null) return fsi1 == null && fsi2 == null;
    return fsi1.GetRealPath().FullName.ToUpper() == fsi2.GetRealPath().FullName.ToUpper();
  }

  public static bool IsEquivalentTo(this FileSystemInfo fsi1, String fsi2) {
    if (fsi1 == null || fsi2 == null) return fsi1 == null && fsi2 == null;
    return fsi1.GetRealPath().FullName.ToUpper() == fsi2.GetRealPath().ToUpper();
  }

  public static bool IsEquivalentTo(this String fsi1, FileSystemInfo fsi2) {
    if (fsi1 == null || fsi2 == null) return fsi1 == null && fsi2 == null;
    return fsi1.GetRealPath().ToUpper() == fsi2.FullName.GetRealPath().ToUpper();
  }

  public static bool IsEquivalentTo(this String fsi1, String fsi2) {
    if (fsi1 == null || fsi2 == null) return fsi1 == null && fsi2 == null;
    return fsi1.GetRealPath().ToUpper() == fsi2.GetRealPath().ToUpper();
  }

  public static bool IsParentOf(this FileSystemInfo fsi1, FileSystemInfo fsi2) {
    if (fsi1 == null || fsi2 == null) return false;
    return fsi2.GetRealPath().FullName.ToUpper().StartsWith(fsi1.GetRealPath().FullName.ToUpper());
  }

  public static bool IsParentOf(this FileSystemInfo fsi1, String fsi2) {
    if (fsi1 == null || fsi2 == null) return false;
    return fsi2.GetRealPath().ToUpper().StartsWith(fsi1.GetRealPath().FullName.ToUpper());
  }

  public static bool IsParentOf(this String fsi1, FileSystemInfo fsi2) {
    if (fsi1 == null || fsi2 == null) return false;
    return fsi2.GetRealPath().FullName.ToUpper().StartsWith(fsi1.GetRealPath().ToUpper());
  }

  public static bool IsParentOf(this String fsi1, String fsi2) {
    if (fsi1 == null || fsi2 == null) return false;
    return fsi2.GetRealPath().ToUpper().StartsWith(fsi1.GetRealPath().ToUpper());
  }

  public static bool IsParentOrEquivalentTo(this FileSystemInfo fsi1, FileSystemInfo fsi2) {
    return fsi1.IsParentOf(fsi2) || fsi1.IsEquivalentTo(fsi2);
  }

  public static bool IsParentOrEquivalentTo(this FileSystemInfo fsi1, String fsi2) {
    return fsi1.IsParentOf(fsi2) || fsi1.IsEquivalentTo(fsi2);
  }

  public static bool IsParentOrEquivalentTo(this String fsi1, FileSystemInfo fsi2) {
    return fsi1.IsParentOf(fsi2) || fsi1.IsEquivalentTo(fsi2);
  }

  public static bool IsParentOrEquivalentTo(this String fsi1, String fsi2) {
    return fsi1.IsParentOf(fsi2) || fsi1.IsEquivalentTo(fsi2);
  }

  public static bool IsChildOf(this FileSystemInfo fsi1, FileSystemInfo fsi2) {
    if (fsi1 == null || fsi2 == null) return false;
    return fsi2.IsParentOf(fsi1);
  }

  public static bool IsChildOf(this FileSystemInfo fsi1, String fsi2) {
    if (fsi1 == null || fsi2 == null) return false;
    return fsi2.IsParentOf(fsi1);
  }

  public static bool IsChildOf(this String fsi1, FileSystemInfo fsi2) {
    if (fsi1 == null || fsi2 == null) return false;
    return fsi2.IsParentOf(fsi1);
  }

  public static bool IsChildOf(this String fsi1, String fsi2) {
    if (fsi1 == null || fsi2 == null) return false;
    return fsi2.IsParentOf(fsi1);
  }

  public static bool IsChildOrEquivalentTo(this FileSystemInfo fsi1, FileSystemInfo fsi2) {
    return fsi1.IsChildOf(fsi2) || fsi1.IsEquivalentTo(fsi2);
  }

  public static bool IsChildOrEquivalentTo(this FileSystemInfo fsi1, String fsi2) {
    return fsi1.IsChildOf(fsi2) || fsi1.IsEquivalentTo(fsi2);
  }

  public static bool IsChildOrEquivalentTo(this String fsi1, FileSystemInfo fsi2) {
    return fsi1.IsChildOf(fsi2) || fsi1.IsEquivalentTo(fsi2);
  }

  public static bool IsChildOrEquivalentTo(this String fsi1, String fsi2) {
    return fsi1.IsChildOf(fsi2) || fsi1.IsEquivalentTo(fsi2);
  }

  [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
  private static extern int GetShortPathName([MarshalAs(UnmanagedType.LPTStr)] String path, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder shortPath, int shortPathLength);

  public static String GetShortPath(this String path) {
    var buf = new StringBuilder(255);
    var status = GetShortPathName(path, buf, buf.Capacity);
    if (status == 0) return null;
    return buf.ToString();
  }

  [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
  private static extern int GetLongPathName([MarshalAs(UnmanagedType.LPTStr)] String path, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder shortPath, int shortPathLength);

  public static String GetLongPath(this String path) {
    var buf = new StringBuilder(255);
    var status = GetLongPathName(path, buf, buf.Capacity);
    if (status == 0) return null;
    return buf.ToString();
  }

  public static bool IsSymlink(this FileSystemInfo fsi) {
    if (fsi == null) return false;
    if (fsi is FileInfo) return ((FileInfo)fsi).IsSymlink();
    if (fsi is DirectoryInfo) return ((DirectoryInfo)fsi).IsSymlink();
    throw new NotSupportedException(String.Format("File system object {0} is not supported", fsi.GetType()));
  }

  public static bool IsSymlink(this FileInfo file) {
    if (file == null) return false;
    return file.Attributes.HasFlag(FileAttributes.ReparsePoint);
  }

  public static bool IsSymlink(this DirectoryInfo dir) {
    if (dir == null) return false;
    return dir.Attributes.HasFlag(FileAttributes.ReparsePoint);
  }

  public static bool IsSymlink(this String path) {
    if (path == null) return false;
    var file = new FileInfo(path);
    if (file.Exists) return file.IsSymlink();

    var dir = new DirectoryInfo(path);
    if (dir.Exists) return dir.IsSymlink();

    return false;
  }

  // copy/pasted from http://chrisbensen.blogspot.com/2010/06/getfinalpathnamebyhandle.html
  private const int FILE_SHARE_READ = 1;
  private const int FILE_SHARE_WRITE = 2;
  private const int CREATION_DISPOSITION_OPEN_EXISTING = 3;
  private const int FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

  // http://msdn.microsoft.com/en-us/library/aa364962%28VS.85%29.aspx
  [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
  private static extern int GetFinalPathNameByHandle(IntPtr handle, [In, Out] StringBuilder path, int bufLen, int flags);

  // http://msdn.microsoft.com/en-us/library/aa363858(VS.85).aspx
  [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
  private static extern SafeFileHandle CreateFile(string lpFileName, int dwDesiredAccess, int dwShareMode, IntPtr SecurityAttributes, int dwCreationDisposition, int dwFlagsAndAttributes, IntPtr hTemplateFile);

  public static FileSystemInfo GetRealPath(this FileSystemInfo fsi) {
    if (fsi == null) return null;
    if (fsi is FileInfo) return ((FileInfo)fsi).GetRealPath();
    if (fsi is DirectoryInfo) return ((DirectoryInfo)fsi).GetRealPath();
    throw new NotSupportedException(String.Format("File system object {0} is not supported", fsi.GetType()));
  }

  public static FileInfo GetRealPath(this FileInfo file) {
    if (file == null) return null;
    return new FileInfo(file.FullName.GetRealPath());
  }

  public static DirectoryInfo GetRealPath(this DirectoryInfo dir) {
    if (dir == null) return null;
    return new DirectoryInfo(dir.FullName.GetRealPath());
  }

  public static String GetRealPath(this String path) {
    if (path == null) return null;
    path = Path.GetFullPath(path);

    for (var parent = Path.GetDirectoryName(path); parent != null; parent = Path.GetDirectoryName(parent)) {
      if (parent.IsSymlink()) {
        var realParent = parent.GetRealPath();
        if (!realParent.EndsWith("\\")) realParent += "\\";
        if (!parent.EndsWith("\\")) parent += "\\";
        var relPath = path.Substring(parent.Length);
        return (realParent + relPath).GetRealPath();
      }
    }

    var file = new FileInfo(path);
    if (file.Exists && !file.Attributes.HasFlag(FileAttributes.ReparsePoint)) return path;
    var dir = new DirectoryInfo(path);
    if (dir.Exists && !dir.Attributes.HasFlag(FileAttributes.ReparsePoint)) return path;

    var directoryHandle = CreateFile(path, 0, 2, IntPtr.Zero, CREATION_DISPOSITION_OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);
    if (directoryHandle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());

    var buf = new StringBuilder(512);
    int size = GetFinalPathNameByHandle(directoryHandle.DangerousGetHandle(), buf, buf.Capacity, 0);
    if (size < 0) throw new Win32Exception(Marshal.GetLastWin32Error());

    // The remarks section of GetFinalPathNameByHandle mentions the return being prefixed with "\\?\"
    // More information about "\\?\" here -> http://msdn.microsoft.com/en-us/library/aa365247(v=VS.85).aspx
    if (buf[0] == '\\' && buf[1] == '\\' && buf[2] == '?' && buf[3] == '\\')
    return buf.ToString().Substring(4);
    else return buf.ToString();
  }
}

public static class Config {
  public static bool sublime;
  public static bool dryrun;
  public static bool verbose;
  public static String action;
  public static String originalTarget;
  public static String target;
  public static Arguments args;

  public static ExitCode parse(String[] args) {
    var flags = args.TakeWhile(arg => arg.StartsWith("/")).Select(flag => flag.ToUpper()).ToList();
    args = args.SkipWhile(arg => arg.StartsWith("/")).ToArray();
    Config.sublime = flags.Contains("/S");
    Config.dryrun = flags.Contains("/D");
    Config.verbose = flags.Contains("/V");

    if (flags.Where(flag => flag == "/?").Count() > 0) {
      Help.printUsage();
      return -1;
    }

    var action = args.Take(1).ElementAtOrDefault(0) ?? "compile";
    args = args.Skip(1).ToArray();
    if (action == null || action == "/?" || action == "-help" || action == "--help") {
      Help.printUsage();
      return -1;
    }

    Config.action = action;

    var target = args.Take(1).ElementAtOrDefault(0) ?? ".";
    args = args.Skip(1).ToArray();
    if (target == null || target == "/?" || target == "-help" || target == "--help") {
      Help.printUsage(action);
      return -1;
    }

    // don't check for file existence - connector might actually create that non-existent file/directory
    Config.originalTarget = Path.GetFullPath(target);
    Config.target = Path.GetFullPath(target);
    Config.args = new Arguments(args.ToList());

    return 0;
  }

  public static void print() {
    Console.println("config: dryrun = {0}, verbose = {1}, action = {2}, target = {3}, args = {4}", dryrun, verbose, action, target, String.Join(", ", args));
  }
}

public static class Help {
  public static void printUsage(String action = null) {
    if (action == null) {
      Console.println();
      Console.println("myke [/D] [/V] [action] [target] [args...]");
      Console.println("  action :  one of \"compile\", \"rebuild\", \"run\", \"repl\", \"test\"; defaults to \"compile\"");
      Console.println("            also supports \"commit\", \"logall\", \"logthis\", \"log\", \"push\", \"pull\"");
      Console.println("  target :  a single file or directory name that will hint what to do next; defaults to \".\"");
      Console.println("            you cannot provide multiple input file/directory names (e.g. as in scalac)");
      Console.println("            this is by design to be bijectively compatible with single-file editors");
      Console.println("            if you do need this, consider configuring your project with some build system");
      Console.println("            and hand-editing myke.cs to integrate with it (e.g. this is how it was done for sbt)");
      Console.println("  args   :  custom data that will be passed to the handler of the command; defaults to \"\"");
      Console.println();
      Console.println("flags:");
      Console.println("  /D     :  dry run, only resolve the connector, but don't do anything; disabled by default");
      Console.println("  /V     :  enables verbose mode of execution; disabled by default");
      Console.println();
      Console.println("see https://raw.github.com/xeno-by/dotwindows/master/Scripts/Scripts for more information");
      Console.println("myke.cs represents the core, while myke-*.cs implement connectors to various build engines");
      Console.println();
      Console.println("connectors:");
    } else {
      Console.println();
      Console.println("action \"{0}\" is supported by the following connectors:", Config.action);
      Console.println();
    }

    printConnectors(action);
  }

  public static void printConnectors(String action = null) {
    var connectors = Connectors.all.Where(connector => action == null || connector.actions().ContainsKey(action)).ToList();
    var maxlen = connectors.Max(connector => connector.name().Length);

    for (var i = 0; i < connectors.Count; ++i) {
      var connector = connectors[i];
      Console.println("* {0} (supports {1})", connector.name(), String.Join(", ",
        connector.actions().Select(kvp => kvp.Key != "default" ? kvp.Key : String.Format("{0} = {1}", kvp.Key, kvp.Value.name()))));
      Console.println(connector.description());
    }
  }
}

public class ConnectorAttribute : Attribute {
  public String name;
  public String description;
  public double priority;
}

public class ActionAttribute : Attribute {
  public String name;

  public ActionAttribute() {
  }

  public ActionAttribute(String name) {
    this.name = name;
  }
}

public class DefaultAttribute : Attribute {
}

public static class Connectors {
  public class ConnectorsComparer : IComparer<Object> {
    public int Compare(Object x, Object y) {
      if (x == null || y == null) return x == null && y == null ? 0 : x == null ? -1 : 1;
      if (!(x is Type) || !(y is Type)) return Compare(x.GetType(), y.GetType());
      return Compare((Type)x, (Type)y);
    }

    public int Compare(Type x, Type y) {
      if (x.priority() > y.priority()) return -1;
      if (x.priority() < y.priority()) return 1;

      for (var x0 = x.BaseType; x0 != null; x0 = x0.BaseType) if (x0 == y) return -1;
      for (var y0 = y.BaseType; y0 != null; y0 = y0.BaseType) if (y0 == x) return 1;
      return 0;
    }
  }

  public static List<Type> all { get {
    var root = Assembly.GetExecutingAssembly();
    var t_connectors = root.GetTypes().Where(t => t.IsDefined(typeof(ConnectorAttribute), true)).ToList();
    t_connectors.Sort(new ConnectorsComparer());
    return t_connectors;
  } }

  public static String name(this Object connector) {
    if (connector is Type) return ((Type)connector).name();
    if (connector is MethodInfo) return ((MethodInfo)connector).name();
    return connector.GetType().name();
  }

  public static String name(this Type connector) {
    var attr = connector.GetCustomAttributes(typeof(ConnectorAttribute), true).Cast<ConnectorAttribute>().Single();
    return attr.name;
  }

  public static String name(this MethodInfo meth) {
    var attr = meth.GetCustomAttributes(typeof(ActionAttribute), true).Cast<ActionAttribute>().Single();
    if (attr.name != null) return attr.name;

    var name = meth.Name;
    var buf = new StringBuilder();
    for (var i = 0; i < name.Length; ++i) {
      var prev = i == 0 ? '\0' : name[i - 1];
      var curr = name[i];
      var next = i == name.Length - 1 ? '\0' : name[i + 1];
      if (Char.IsLower(prev) && Char.IsUpper(curr) && !Char.IsUpper(next)) {
        buf.Append("-");
        buf.Append(Char.ToLower(curr));
      } else {
        buf.Append(curr);
      }
    }

    return buf.ToString();
  }

  public static String description(this Object connector) {
    if (connector is Type) return ((Type)connector).description();
    return connector.GetType().description();
  }

  public static String description(this Type connector) {
    var attr = connector.GetCustomAttributes(typeof(ConnectorAttribute), true).Cast<ConnectorAttribute>().Single();
    return attr.description;
  }

  public static double priority(this Object connector) {
    if (connector is Type) return ((Type)connector).priority();
    return connector.GetType().priority();
  }

  public static double priority(this Type connector) {
    var attr = connector.GetCustomAttributes(typeof(ConnectorAttribute), true).Cast<ConnectorAttribute>().Single();
    return attr.priority;
  }

  public static Dictionary<String, MethodInfo> actions(this Type connector) {
    var methods = connector.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.IsDefined(typeof(ActionAttribute), true)).ToList();
    var map = methods.ToDictionary(meth => meth.name(), meth => meth);

    methods.ForEach(meth => {
      var attr = meth.GetCustomAttributes(typeof(DefaultAttribute), true).Cast<DefaultAttribute>().SingleOrDefault();
      if (attr != null) map.Add("default", meth);
    });

    return map;
  }

  public static Dictionary<String, Func<ExitCode>> actions(this Object connector) {
    var t_actions = connector.GetType().actions();
    return t_actions.Keys.ToDictionary<String, String, Func<ExitCode>>(key => key, key => {
      var method = t_actions[key];
      var args = method.bindArgs();
      return () => (ExitCode)method.Invoke(connector, args);
    });
  }

  public static Object instantiate(this Type connector) {
    var ctors = connector.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(ctor1 => ctor1.GetParameters().Length > 0).ToList();
    var binds = ctors.ToDictionary(ctor1 => ctor1, ctor1 => ctor1.bindArgs());
    var bind = binds.Where(kvp => kvp.Value != null).SingleOrDefault();
    var ctor = bind.Key;
    var args = bind.Value;
    return args == null ? null : ctor.Invoke(args);
  }

  public static bool accept(this Object connector) {
    var method = connector.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.Name == "accept").Single();
    var args = method.bindArgs();
    return args == null ? false : (bool)method.Invoke(connector, args);
  }

  public static ExitCode action(this Object connector, String action) {
    return connector.actions()[action]();
  }

  private static Object[] bindArgs(this MethodBase method) {
    try {
      return method.GetParameters().Select<ParameterInfo, Object>(p => {
        if (p.ParameterType == typeof(FileInfo)) {
          var file = new FileInfo(Config.target);
          if (!file.Exists) throw new FileNotFoundException(Config.target);
          return file;
        } else if (p.ParameterType == typeof(DirectoryInfo)) {
          var dir = new DirectoryInfo(Config.target);
          if (!dir.Exists) throw new DirectoryNotFoundException(Config.target);
          return dir;
        } else if (p.ParameterType == typeof(Lines)) {
          var file = new FileInfo(Config.target);
          if (!file.Exists) throw new FileNotFoundException(Config.target);
          return new Lines(file, File.ReadAllLines(file.FullName).ToList());
        } else if (p.ParameterType == typeof(Arguments)) {
          return Config.args;
        } else {
          throw new NotSupportedException(String.Format("cannot bind parameter {0} of type {1}", p.Name, p.ParameterType.FullName));
        }
      }).ToArray();
    } catch (Exception) {
      return null;
    }
  }
}

[DebuggerDisplay("Count = {Count}")]
[DebuggerTypeProxy("System.Collections.Generic.Mscorlib_CollectionDebugView`1,mscorlib,Version=2.0.0.0,Culture=neutral,PublicKeyToken=b77a5c561934e089")]
[DebuggerNonUserCode]
public abstract class BaseList<T> : IList<T> {
  protected abstract IEnumerable<T> Read();

  public virtual bool IsReadOnly { get { return true; } }
  protected virtual void InsertAt(int index, T el) {}
  protected virtual void UpdateAt(int index, T el) {}
  public virtual void RemoveAt(int index) {}

  IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
  public IEnumerator<T> GetEnumerator() {
    return Read().GetEnumerator();
  }

  public void Add(T item) {
    if (IsReadOnly) throw new NotSupportedException("cannot add to a readonly list");
    Insert(Count, item);
  }

  public void Clear() {
    if (IsReadOnly) throw new NotSupportedException("cannot clear a readonly list");
    for (var i = 0; i < Count; ++i) RemoveAt(0);
  }

  public bool Contains(T item) {
    return IndexOf(item) != -1;
  }

  void ICollection<T>.CopyTo(T[] array, int arrayIndex) {
    Read().ToArray().CopyTo(array, arrayIndex);
  }

  public bool Remove(T item) {
    if (IsReadOnly) throw new NotSupportedException("cannot remove from a readonly list");

    var index = IndexOf(item);
    if (index == -1) {
      return false;
    } else {
      RemoveAt(index);
      return true;
    }
  }

  public int Count {
    get { return Read().Count(); }
  }

  public int IndexOf(T item) {
    return Read().ToList().IndexOf(item);
  }

  public void Insert(int index, T item) {
    if (IsReadOnly) throw new NotSupportedException("cannot insert into a readonly list");
    InsertAt(index, item);
  }

  public T this[int index] {
    get { return Read().ElementAt(index); }
    set {
      if (IsReadOnly) throw new NotSupportedException("cannot update a readonly list");
      UpdateAt(index, value);
    }
  }
}

public class Lines : BaseList<String> {
  public FileInfo file;
  private List<String> lines;

  public Lines(FileInfo file, List<String> lines) {
    this.file = file;
    this.lines = lines;
  }

  protected override IEnumerable<String> Read() {
    return lines;
  }
}

public class Arguments : BaseList<String> {
  private List<String> arguments;

  public Arguments(List<String> arguments) {
    this.arguments = arguments;
  }

  protected override IEnumerable<String> Read() {
    return arguments;
  }

  public override String ToString() {
    return String.Join(", ", arguments.Select(arg => arg.ShellEscape()));
  }
}


public abstract class Prj {
  public FileInfo file;
  public DirectoryInfo dir;

  public Prj() : this((DirectoryInfo)null) {
    initEnv();
  }

  public Prj(FileInfo file) {
    this.file = file;
    this.dir = file == null ? null : file.Directory;
    initEnv();
  }

  public Prj(DirectoryInfo dir) {
    this.file = null;
    this.dir = dir ?? (project == null ? null : new DirectoryInfo(project));
    initEnv();
  }

  public virtual String project { get { return null; } }

  public virtual DirectoryInfo root { get {
    if (project != null) {
      return new DirectoryInfo(project);
    }

    return dir;
  } }

  [Action]
  public virtual ExitCode console() {
    return Console.interactive("mycmd.exe", home: root);
  }

  public abstract bool accept();

  protected static ExitCode print(String format, params Object[] objs) {
    Console.print(format, objs);
    return 0;
  }

  protected static ExitCode print(String obj) {
    Console.print(obj);
    return 0;
  }

  protected static ExitCode print(Object obj) {
    Console.print(obj);
    return 0;
  }

  protected static ExitCode println(String format, params Object[] objs) {
    Console.println(format, objs);
    return 0;
  }

  protected static ExitCode println(String obj) {
    Console.println(obj);
    return 0;
  }

  protected static ExitCode println(Object obj) {
    Console.println(obj);
    return 0;
  }

  protected static ExitCode println() {
    Console.println();
    return 0;
  }

  private Dictionary<String, String> _env;
  public Dictionary<String, String> env { get { return _env; } }
  private void initEnv() {
    var currentEnv = AppDomain.CurrentDomain.GetData("%%MykeEnv") as Dictionary<String, String>;
    _env = currentEnv = currentEnv ?? new Dictionary<String, String>();
    AppDomain.CurrentDomain.SetData("%%MykeEnv", _env);
  }
}

public abstract class Git : Prj {
  public Git() : base((DirectoryInfo)null) {}
  public Git(FileInfo file) : base(file) {}
  public Git(DirectoryInfo dir) : base(dir) {}

  public override DirectoryInfo root { get {
    if (project != null) {
      return new DirectoryInfo(project);
    }

    // note. this leads to dubious results. don't put it here
    //if (repo != null) {
    //  return repo;
    //}

    return dir;
  } }

  public DirectoryInfo repo { get {
    // todo. do we need to cache this?
    return detectRepo();
  } }

  public virtual DirectoryInfo detectRepo() {
    var wannabe = file != null ? file.Directory : dir;
    if (wannabe.IsChildOrEquivalentTo("%SCRIPTS_HOME%".Expand())) return new DirectoryInfo(@"%SOFTWARE%".Expand());

    while (wannabe != null) {
      var gitIndex = wannabe.GetDirectories().FirstOrDefault(child => child.Name == ".git");
      if (gitIndex != null) return wannabe;
      wannabe = wannabe.Parent;
    }

    return null;
  }

  public virtual bool verifyRepo() {
    if (repo == null) {
      Console.println("error: {0} is not under Git repository", file != null ? file.FullName : dir.FullName);
      Console.print("Create the repo with the project root (y/n)? ");
      var answer = Console.readln();
      if (answer == "" || answer.ToLower() == "y" || answer.ToLower() == "yes") {
        Console.batch("git init", home: root);
        return true;
      } else {
        return false;
      }
    } else {
      return true;
    }
  }

  [Action]
  public virtual ExitCode commit() {
    if (dir.IsChildOrEquivalentTo("%SCRIPTS_HOME%".Expand())) {
      var status = Console.batch("save-settings.bat");
      if (status != 0) return -1;
    }

    if (!verifyRepo()) return -1;
    return Console.ui(String.Format("tgit commit \"{0}\"", repo.GetRealPath().FullName));
  }

  [Action]
  public virtual ExitCode logall() {
    if (!verifyRepo()) return -1;
    return Console.ui(String.Format("tgit log \"{0}\"", repo.GetRealPath().FullName));
  }

  [Action]
  public virtual ExitCode logthis() {
    if (!verifyRepo()) return -1;
    return Console.ui(String.Format("tgit log \"{0}\"", (file != null ? (FileSystemInfo)file : dir).GetRealPath().FullName));
  }

  [Action]
  public virtual ExitCode blame() {
    if (!verifyRepo()) return -1;
    return Console.ui(String.Format("tgit blame \"{0}\"", (file != null ? (FileSystemInfo)file : dir).GetRealPath().FullName));
  }

  [Action]
  public virtual ExitCode log() {
    return logall();
  }

  [Action]
  public virtual ExitCode push() {
    if (!verifyRepo()) return -1;
    return Console.interactive("git push", home: repo.GetRealPath());
  }

  [Action]
  public virtual ExitCode pull() {
    if (!verifyRepo()) return -1;
    return Console.interactive("git pull", home: repo.GetRealPath());
  }
}
