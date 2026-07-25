@echo off

(
    echo archive .\track_content_universal.dz
    echo basedir .\texture\
    for %%f in (
	texture\*.png
	texture\*.jpg
	texture\*.jpeg
	texture\*.plist
    ) do (
        echo file %%~nxf 0 dz
    )
) > config.dcl

dzip.exe config.dcl