@echo off

(
    echo archive .\GUI_2048_1536.dz
    echo basedir .\gui\
    for %%f in (
	gui\*.png
	gui\*.jpg
	gui\*.jpeg
	gui\*.plist
    ) do (
        echo file %%~nxf 0 dz
    )
) > config.dcl

dzip.exe config.dcl