@echo off

(
    echo archive .\common_xml.dz
    echo basedir .\common\
    for %%f in (common\*.xml) do (
        echo file %%~nxf 0 dz
    )
) > config.dcl

dzip.exe config.dcl