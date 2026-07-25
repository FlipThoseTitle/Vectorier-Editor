@echo off

(
    echo archive .\level_xml.dz
    echo basedir .\level\level_xml\
    for %%f in (level\level_xml\*.xml) do (
        echo file %%~nxf 0 zlib
    )
) > config.dcl

dzip.exe config.dcl