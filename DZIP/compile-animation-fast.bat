@echo off

(
    echo archive .\animations.dz
    echo basedir .\animation\
    for %%f in (
	animation\*.bin
	animation\*.bytes
    ) do (
        echo file %%~nxf 0 zlib
    )
) > config.dcl

dzip.exe config.dcl