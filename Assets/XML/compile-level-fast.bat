echo archive .\output\level_xml.dz>config.dcl
echo basedir .\input\>>config.dcl
FOR /F %%f IN ('dir /B input\') DO echo file %%f 0 zlib>>config.dcl
dzip.exe config.dcl