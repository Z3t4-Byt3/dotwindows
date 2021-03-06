// build this with "csc /r:Microsoft.VisualBasic.dll /r:ZetaLongPaths.dll /r:LibGit2Sharp.dll /t:exe /out:myke.exe /debug+ myke*.cs"

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
  "Wraps the development workflow of scalatest donor for Kepler")]
public class Donor : Kep {
  public String donneeName() { return "Kepler"; }
  public Kep donnee() { return new Kep(); }
  public override String project { get { return @"%PROJECTS%\Donor".Expand(); } }
  protected override String transplantTo { get { return donnee().project; } }

  public override String profile { get { return profileAlt; } }
  public override String profileClean { get { return profileAltClean; } }
  public override String profileLibrary { get { return profileAltLibrary; } }
  public override String profileReflect { get { return profileAltReflect; } }
  public override String profileCompiler { get { return profileAltCompiler; } }

  public Donor() : base() { }
  public Donor(FileInfo file, Arguments arguments) : base(file, arguments) { this.arguments = arguments; }
  public Donor(DirectoryInfo dir, Arguments arguments) : base(dir, arguments) { this.arguments = arguments; }

  [Default, Action]
  public override ExitCode compile() {
    if (inPlayground || inTest) {
      return base.compile();
    } else {
      var status = Console.batch("ant " + profile + " -buildfile build.xml", home: project);
      if (!status) return -1;

      println();
      println("Transplanting partest to " + donneeName() + "...");
      status = status && transplantDir("build/quick/classes/partest", "build/locker/classes/partest");
      status = status && transplantDir("build/quick/classes/library/scala/actors", "build/locker/classes/library/scala/actors");
      // status = status && transplantDir("build/pack/lib/scala-actors.jar/scala/actors", "build/locker/classes/library/scala/actors");
      status = status && transplantDir("build/quick/classes/scalap/scala/tools/scalap", "build/locker/classes/partest/scala/tools/scalap");
      status = status && transplantDir("build/quick/classes/scalacheck/org/scalacheck", "build/locker/classes/partest/org/scalacheck");
      status = status && transplantFile("build/pack/misc/scala-devel/plugins/continuations.jar", "build/locker/classes/continuations.jar");
      status = status && transplantDir("build/pack/misc/scala-devel/plugins/continuations.jar", "build/locker/classes/continuations");
      status = status && transplantDir("build/quick/classes/library/scala/util/continuations", "build/locker/classes/library/scala/util/continuations");
      status = status && transplantDir("lib/forkjoin.jar/scala/concurrent/forkjoin", "build/locker/classes/library/scala/concurrent/forkjoin");
      status = status && transplantDir("lib/forkjoin.jar/scala/concurrent/util", "build/locker/classes/library/scala/concurrent/util", overwrite: false);
      status = status && transplantDir("build/asm/classes/scala/tools/asm", "build/locker/classes/compiler/scala/tools/asm");
      status = status && transplantDir("lib/fjbg.jar/ch/epfl/lamp/fjbg", "build/locker/classes/compiler/ch/epfl/lamp/fjbg");
      status = status && transplantDir("lib/fjbg.jar/ch/epfl/lamp/util", "build/locker/classes/compiler/ch/epfl/lamp/util");
      status = status && transplantDir("lib/msil.jar/ch/epfl/lamp/compiler/msil", "build/locker/classes/compiler/ch/epfl/lamp/compiler/msil");
      status = status && transplantDir("lib/jline.jar/org/fusesource", "build/locker/classes/library/org/fusesource");
      status = status && transplantDir("lib/jline.jar/scala/tools/jline", "build/locker/classes/library/scala/tools/jline");

      if (status) {
        println();
        println("Calculating longest path lengths in " + donneeName() + "...");
        var locker = new ZlpDirectoryInfo(donnee().project + "\\build\\locker" );
        var classes = locker.GetFiles("*.class", SearchOption.AllDirectories).ToList();
        classes = classes.OrderByDescending(f => f.FullName.Length).ToList();
        classes = classes.Take(5).Concat(classes.Skip(5).Where(f => f.FullName.Length > 260)).ToList();
        classes.ForEach(f => println(String.Format("  * {0} {1}", f.FullName.Length, f.FullName)));
        if (classes[0].FullName.Length > 260) {
          println("WARNING: THERE ARE FILES WITH NAMES LONGER THAN 260 CHARACTERS, JVM WON'T BE ABLE TO LOAD THEM UNLESS THEY ARE PACKED INTO A JAR");
          //return -1;
        }

        println();
        status = status && donnee().packLockerIntoJars();

        // works around an infrastructure bug that I'm cba to fix right know
        // transplantFile(@"C:\Projects\scala-partest.jar", "/build/locker/lib/scala-partest.jar");
      }

      return status;
    }
  }
}