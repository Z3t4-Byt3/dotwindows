// build this with "csc /r:Microsoft.VisualBasic.dll /r:ZetaLongPaths.dll /t:exe /out:myke.exe /debug+ myke*.cs"

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using ZetaLongPaths;

[Connector(name = "donor", priority = 999, description =
  "Wraps the development workflow of donor scalacheck for Kepler")]

public class Donor : Kep {
  public override String project { get { return @"%PROJECTS%\Donor".Expand(); } }
  public override String profile { get { return "build"; } }

  public override bool accept() {
    if (Config.verbose) println("project = {0}, dir = {1}", project.Expand(), dir.FullName);
    return dir.IsChildOrEquivalentTo(project);
  }

  public Donor() : base() {}
  public Donor(FileInfo file, Arguments arguments) : base(file, arguments) { this.arguments = arguments; }
  public Donor(DirectoryInfo dir, Arguments arguments) : base(dir, arguments) { this.arguments = arguments; }

  [Default, Action]
  public override ExitCode compile() {
    if (inPlayground || inTest) {
      return base.compile();
    } else {
      var status = Console.batch("ant " + profile + " -buildfile build.xml", home: root);
      if (!status) return -1;

      println();
      println("Transplanting partest to Kepler...");
      status = status && transplantDir("build/quick/classes/partest", "build/locker/classes/partest");
      status = status && transplantDir("build/quick/classes/library/scala/actors", "build/locker/classes/partest/scala/actors");
      status = status && transplantDir("build/quick/classes/scalap/scala/tools/scalap", "build/locker/classes/partest/scala/tools/scalap");
      status = status && transplantDir("build/quick/classes/scalacheck/org/scalacheck", "build/locker/classes/partest/org/scalacheck");
      status = status && transplantFile("build/pack/misc/scala-devel/plugins/continuations.jar", "build/locker/classes/continuations.jar");
      status = status && transplantDir("build/pack/misc/scala-devel/plugins/continuations.jar", "build/locker/classes/continuations");
      status = status && transplantDir("build/quick/classes/library/scala/util/continuations", "build/locker/classes/library/scala/util/continuations");
      status = status && transplantDir("lib/forkjoin.jar/scala/concurrent/forkjoin", "build/locker/classes/partest/scala/concurrent/forkjoin");
      status = status && transplantDir("lib/fjbg.jar/ch/epfl/lamp/fjbg", "build/locker/classes/partest/ch/epfl/lamp/fjbg");
      status = status && transplantDir("lib/fjbg.jar/ch/epfl/lamp/util", "build/locker/classes/partest/ch/epfl/lamp/util");
      status = status && transplantDir("lib/msil.jar/ch/epfl/lamp/compiler/msil", "build/locker/classes/partest/ch/epfl/lamp/compiler/msil");
      status = status && transplantDir("lib/jline.jar/org/fusesource", "build/locker/classes/partest/org/fusesource");
      status = status && transplantDir("lib/jline.jar/scala/tools/jline", "build/locker/classes/partest/scala/tools/jline");
      return status;
    }
  }

  public ExitCode transplantFile(String from, String to) {
    from = project + "\\" + from.Replace("/", "\\");
    var to5 = new Kep5().project + "\\" + to.Replace("/", "\\");
    to = new Kep().project + "\\" + to.Replace("/", "\\");
    print("  * Copying {0} to {1}... ", from, to);

    try {
      ExitCode status = -1;
      if (File.Exists(from)) status = CopyFile(from, to) && CopyFile(from, to5);
      if (status) println("[  OK  ]");
      return status;
    } catch (Exception ex) {
      println("[FAILED]");
      println(ex);
      return -1;
    }
  }

  public static ExitCode CopyFile(string sourceFile, string destFile) {
    try {
      File.Copy(sourceFile, destFile, true);
      return 0;
    } catch (Exception ex) {
      println("[FAILED]");
      println(ex);
      return -1;
    }
  }

  public ExitCode transplantDir(String from, String to) {
    from = project + "\\" + from.Replace("/", "\\");
    var to5 = new Kep5().project + "\\" + to.Replace("/", "\\");
    to = new Kep().project + "\\" + to.Replace("/", "\\");
    print("  * Copying {0} to {1}... ", from, to);

    try {
      ExitCode status = -1;
      status = CopyDirectory(from, to) && CopyDirectory(from, to5);
      if (status) println("[  OK  ]");
      return status;
    } catch (Exception ex) {
      println("[FAILED]");
      println(ex);
      return -1;
    }
  }

  public static ExitCode CopyDirectory(string sourceFolder, string destFolder) {
    try {
      int iof = -1;
      while ((iof = sourceFolder.IndexOf(".jar")) != -1) {
        var archive = sourceFolder.Substring(0, iof + 4);
        var insideArchive = sourceFolder.Substring(iof + 4);
        var temp = Path.GetTempFileName() + ".unpack";
        var status = Console.batch(String.Format("unzip -qq \"{0}\" -d \"{1}\"", archive, temp));
        if (!status) { println("[FAILED]"); return status; }
        sourceFolder = temp + "\\" + insideArchive;
      }

      if (ZlpIOHelper.DirectoryExists(destFolder)) ZlpIOHelper.DeleteDirectory(destFolder, true);
      if (!ZlpIOHelper.DirectoryExists(destFolder)) ZlpIOHelper.CreateDirectory(destFolder);

      var files = new ZlpDirectoryInfo(sourceFolder).GetFiles();
      foreach (var file in files) {
        var name = ZlpPathHelper.GetFileNameFromFilePath(file.FullName);
        var dest = ZlpPathHelper.Combine(destFolder, name);
        file.CopyTo(dest, true);
      }

      var folders = new ZlpDirectoryInfo(sourceFolder).GetDirectories();
      foreach (var folder in folders) {
        var name = ZlpPathHelper.GetFileNameFromFilePath(folder.FullName);
        var dest = ZlpPathHelper.Combine(destFolder, name);
        var status = CopyDirectory(folder.FullName, dest);
        if (!status) { println("[FAILED]"); return status; }
      }

      return 0;
    } catch (Exception ex) {
      println("[FAILED]");
      println(ex);
      return -1;
    }
  }
}