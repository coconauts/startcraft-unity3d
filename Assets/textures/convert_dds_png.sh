#!/bin/bash

for file in *.dds
do
  newfile="$(basename "$file" .dds).png"
  echo "Converting $file into $newfile" 
   convert "$file" "$newfile"
done