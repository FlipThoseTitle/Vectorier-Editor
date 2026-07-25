@echo off

(
    echo archive .\sound.dz
    echo basedir .\sound\
    for %%f in (sound\*.wav) do (
        echo file %%~nxf 0 zlib
    )
) > config.dcl

dzip.exe config.dcl