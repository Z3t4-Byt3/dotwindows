@echo off
java -cp "C:\Projects\Kepler\lib\jline.jar;C:\Projects\Kepler\lib\fjbg.jar;C:\Projects\Kepler\build\locker\classes\compiler;C:\Projects\Kepler\build\locker\classes\library" -Dscala.usejavacp=true scala.tools.nsc.Main %*