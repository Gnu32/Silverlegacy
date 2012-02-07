@ECHO OFF

echo        o 8                      ooooo  o       
echo          8                      8              
echo .oPYo. o8 8 o    o .oPYo. oPYo. o8oo   o8 odYo. 
echo Yb..    8 8 Y.  .P 8oooo8 8  `'  8      8 8' `8 
echo   'Yb.  8 8 `b..d' 8.     8      8      8 8   8 
echo `YooP'  8 8  `YP'  `Yooo' 8      8      8 8   8 
echo :.....::....::...:::.....:..:::::..:::::....::..
echo ::::::::::::::::::::::::Major Rasputin 2012:::::
echo.

rem ## Default Visual Studio choice (2008, 2010)
set vstudio=2010

rem ## Default .NET Framework (3_5, 4_0 (Unsupported on VS2008))
set framework=4_0

rem ## Default architecture (86 (for 32bit), 64, AnyCPU)
set bits=AnyCPU

rem ## Generate a compile batch file? (y ,n)
set compilebatch=n

rem ## Default "run compile batch" choice (y (requires compilebatch to be set to y), n)
set compile_at_end=n

echo I will now ask you three questions regarding your build.
echo However, if you wish to build for:
echo		Visual Studio %vstudio%
echo		.NET Framework %framework%
echo		%bits%x Architecture
if %compilebatch%==y echo With a compile batch file generated at the end
if %compile_at_end%==y echo And you would like to compile straight after prebuild...
echo.
echo Simply tap [ENTER] four times.
echo.
echo Note that you can change these defaults by opening this batch file in a text editor.
echo.

:vstudio
set /p vstudio="Choose your Visual Studio version (2008, 2010) [%vstudio%]: "
if %vstudio%==2008 goto framework
if %vstudio%==2010 goto framework
echo "%vstudio%" isn't a valid choice!
goto vstudio

:framework
set /p framework="Choose your .NET framework (3_5, 4_0 (Unsupported on VS2008)) [%framework%]: "
if %framework%==3_5 goto bits
if %framework%==4_0 goto frameworkcheck
echo "%framework%" isn't a valid choice!
goto framework

	:frameworkcheck
	if %vstudio%==2008 goto frameworkerror
	goto bits

	:frameworkerror
	echo Sorry! Visual Studio 2008 only supports 3_5.
	goto framework

:bits
set /p bits="Choose your architecture (AnyCPU, x86, x64) [%bits%]: "
if %bits%==86 goto final
if %bits%==x86 goto final
if %bits%==64 goto final
if %bits%==x64 goto final
if %bits%==AnyCPU goto final
echo "%bits%" isn't a valid choice!
goto bits

:final
echo.
echo.

echo Calling Prebuild for target %vstudio% with framework %framework%...
Tools\Prebuild.exe /target vs%vstudio% /targetframework v%framework%

if exist Compile.*.bat (
	echo Deleting previous compile batch file...
	echo.
	del Compile.*.bat
)

if %compilebatch%==n goto eof

echo.
echo Creating compile batch file for your convinence...
if %framework%==3_5 set fpath=C:\WINDOWS\Microsoft.NET\Framework\v3.5\msbuild
if %framework%==4_0 set fpath=C:\WINDOWS\Microsoft.NET\Framework\v4.0.30319\msbuild
if %bits%==64 set args=/p:Platform="x64"
if %bits%==86 set args=/p:Platform="x86"
if %bits%==x64 set args=/p:Platform="x64"
if %bits%==x86 set args=/p:Platform="x86"
set filename=Compile.VS%vstudio%.net%framework%.x%bits%.bat

echo %fpath% %args% > %filename% /p:DefineConstants=ISWIN

echo.
set /p compile_at_end="Done, %filename% created. Compile now? (y,n) [%compile_at_end%]"
if %compile_at_end%==y (
	%filename%
	pause
)

:eof