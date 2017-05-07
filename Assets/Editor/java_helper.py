import os, sys
import subprocess
import re
import os_helper
from subprocess import call

def create_import(package_to_import, file_path):
  print "\n* Import {0} into {1}".format(package_to_import, file_path)
  os_helper.insert_after(file_path, "package ", ['import {0};'.format(package_to_import)])

def insert_oncreate(file_path, codeList):
	print "\n* Insert {0} into onCreate".format(codeList)
	os_helper.insert_after(file_path, "playerView.requestFocus();", codeList)
	
def insert_ondestroy(file_path, codeList):
	print "\n* Insert {0} into onDestroy".format(codeList)
	os_helper.insert_after(file_path, "super.onDestroy();", codeList)
	
def insert_onpause(file_path, codeList):
	print "\n* Insert {0} into onPause".format(codeList)
	os_helper.insert_after(file_path, "mUnityPlayer.pause();", codeList)
	
def insert_onresume(file_path, codeList):
	print "\n* Insert {0} into onResume".format(codeList)
	os_helper.insert_after(file_path, "mUnityPlayer.resume();", codeList)
	
def add_activity(file_path, codeList):
  print "\n* Adding activity into {0}".format(file_path)
  os_helper.insert_before(file_path, "</application>", codeList)
  
def add_into_application_tag(file_path, codeList):
  print "\n* Adding code into <application></application>"
  add_activity(file_path, codeList)
  
def add_permission(file_path, permission_name):
  full_permission_name = '  <uses-permission android:name="{0}" />'.format(permission_name)
  print "\n* Adding permission {0}".format(full_permission_name)
  os_helper.insert_before(file_path, '<uses-feature android:name="android.hardware.touchscreen" />', [full_permission_name])
  
def add_string_resource(file_path, string_key, string_value):
  print "\n* Adding string resource into {0}".format(file_path)
  os_helper.insert_before(os.path.join(file_path, "res/values/strings.xml"), "</resources>", ['<string name="{0}">{1}</string>'.format(string_key, string_value)])
