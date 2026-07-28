@echo off

(
    echo archive .\level_xml.dz
    echo basedir .\level\
    for %%f in (level\*.xml) do (
        echo file %%~nxf 0 zlib
    )
) > config.dcl

dzip.exe config.dcl