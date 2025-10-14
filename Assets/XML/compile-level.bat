@echo off
echo archive .\level_xml.dz>config.dcl
echo basedir .\level_xml\>>config.dcl
for %%f in (level_xml\*.xml) do (
    if /I not "%%~xf"==".meta" echo file %%~nxf 0 dz>>config.dcl
)
dzip.exe config.dcl