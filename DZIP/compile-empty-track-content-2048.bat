@echo off

(
    echo archive .\track_content_2048.dz
    echo basedir .\_TEMPLATE\empty\
) > config.dcl

dzip.exe config.dcl