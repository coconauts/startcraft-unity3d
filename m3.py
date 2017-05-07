#!/usr/bin/python3
# -*- coding: utf-8 -*-

# ##### BEGIN GPL LICENSE BLOCK #####
#
#  This program is free software; you can redistribute it and/or
#  modify it under the terms of the GNU General Public License
#  as published by the Free Software Foundation; either version 2
#  of the License, or (at your option) any later version.
#
#  This program is distributed in the hope that it will be useful,
#  but WITHOUT ANY WARRANTY; without even the implied warranty of
#  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
#  GNU General Public License for more details.
#
#  You should have received a copy of the GNU General Public License
#  along with this program; if not, write to the Free Software Foundation,
#  Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
#
# ##### END GPL LICENSE BLOCK #####

import xml.dom.minidom
from xml.dom.minidom import Node
import re
from sys import stderr
import struct

def increaseToValidSectionSize(size):
    blockSize = 16
    incompleteBlockBytes = (size % blockSize)
    if incompleteBlockBytes != 0:
        missingBytesToCompleteBlock = blockSize - incompleteBlockBytes
        return size + missingBytesToCompleteBlock
    else:
        return size

class Section:
    """Has fields indexEntry and structureDescription and sometimes also the fields rawBytes and content """
    
    def __init__(self):
        self.timesReferenced = 0
    
    def determineContentField(self, checkExpectedValue):
        indexEntry = self.indexEntry
        self.content = self.structureDescription.createInstances(buffer=self.rawBytes, count=indexEntry.repetitions, checkExpectedValue=checkExpectedValue)

    def determineFieldRawBytes(self):
        minRawBytes = self.determineRawBytesWithData()
        if len(minRawBytes) != self.bytesRequiredForContent():
            raise Exception("Section size calculation failed: Expected %s but was %s for %s; content: %s" % (self.bytesRequiredForContent(), len(minRawBytes), self.structureDescription.structureName, minRawBytes))
        sectionSize = increaseToValidSectionSize(len(minRawBytes))
        if len(minRawBytes) == sectionSize:
            self.rawBytes = minRawBytes
        else:
            rawBytes = bytearray(sectionSize)
            rawBytes[0:len(minRawBytes)] = minRawBytes
            for i in range(len(minRawBytes),sectionSize):
                rawBytes[i] = 0xaa
            self.rawBytes = rawBytes
            
    def determineRawBytesWithData(self):
        return self.structureDescription.instancesToBytes(self.content)
    
    def bytesRequiredForContent(self):
        return self.structureDescription.countBytesRequiredForInstances(self.content)
    
    def resolveReferences(self, sections):
        if not self.structureDescription.isPrimitive:
            for object in self.content:
                object.resolveReferences(sections)


primitiveFieldTypeSizes = {"uint32":4,"int32":4,"uint16":2,"int16":2, "uint8":1, "int8":1, "float":4, "tag":4, "fixed8": 1}
primitiveFieldTypeFormats = {"uint32":"I","int32":"i","uint16":"H","int16":"h", "uint8":"B", "int8":"b" , "float":"f", "tag":"4s", "fixed8": "B"}
intTypes = {"uint32","int32","uint16","int16", "uint8", "int8"}

structureNamesOfPrimitiveTypes = set(["CHAR", "U8__", "REAL", "I16_", "U16_", "I32_", "U32_", "FLAG"])

class M3StructureHistory:
    "Describes the history of a structure with a specific name"
    
    def __init__(self, name, versionToSizeMap, allFields):
        self.name = name
        self.versionToSizeMap = versionToSizeMap
        self.allFields = allFields
        self.versionToStructureDescriptionMap = {}
        self.isPrimitive = self.name in structureNamesOfPrimitiveTypes

    def getVersion(self, version):
        structure = self.versionToStructureDescriptionMap.get(version)
        if structure == None:
            usedFields = []
            for field in self.allFields:
                includeField = True
                if field.sinceVersion != None and version < field.sinceVersion:
                    includeField = False
                if field.tillVersion != None and version > field.tillVersion:
                    includeField = False
                if includeField:
                    usedFields.append(field)
            specifiedSize = self.versionToSizeMap.get(version)  
            if specifiedSize == None:
                return None
            structure = M3StructureDescription(self.name, version, usedFields, specifiedSize, self)
            self.versionToStructureDescriptionMap[version] = structure
        return structure
    
    def getNewestVersion(self):
        newestVersion = None
        for version in self.versionToSizeMap.keys():
            if newestVersion == None or version > newestVersion:
                newestVersion = version
        return self.getVersion(newestVersion)
    
    def createEmptyArray(self):
        if self.name == "CHAR":
            return None # even no terminating character
        elif self.name == "U8__":
            return bytearray(0)
        else:
            return []
        
        def __str__():
            return self.name

class M3StructureDescription:

    def __init__(self, structureName, structureVersion, fields, specifiedSize, history):
        self.structureName = structureName
        self.structureVersion = structureVersion
        self.fields = fields
        self.size = specifiedSize
        self.isPrimitive = self.structureName in structureNamesOfPrimitiveTypes
        self.history = history
        
        # Validate the specified size:
        calculatedSize = 0
        for field in fields:
            calculatedSize += field.size
        if calculatedSize != specifiedSize:
            self.dumpOffsets()
            raise Exception("Size mismatch: %s in version %d has been specified to have size %d, but the calculated size was %d" % (structureName, structureVersion, specifiedSize, calculatedSize))

        nameToFieldMap = {}
        for field in fields:
            nameToFieldMap[field.name] = field
        self.nameToFieldMap = nameToFieldMap

    def createInstance(self, buffer=None, offset=0, checkExpectedValue=True):
        return M3Structure(self, buffer, offset, checkExpectedValue)

    def createInstances(self, buffer, count, checkExpectedValue=True):
        if self.isPrimitive:
            if self.structureName == "CHAR":
                return buffer[:count-1].decode("ASCII")
            elif self.structureName == "U8__":
                return bytearray(buffer[:count])
            else:
                structFormat = self.fields[0].structFormat
                list = []
                for offset in range(0, count*self.size, self.size):
                    bytesOfOneEntry = buffer[offset:(offset+self.size)]
                    intValue = structFormat.unpack(bytesOfOneEntry)[0]
                    list.append(intValue)
                return list
        else:
            list = []
            instanceOffset = 0
            for i in range(count):
                list.append(self.createInstance(buffer=buffer, offset=instanceOffset, checkExpectedValue=checkExpectedValue));
                instanceOffset += self.size
            return list
    
    def dumpOffsets(self):
        offset = 0
        stderr.write("Offsets of %s in version %d:\n" % (self.structureName, self.structureVersion))
        for field in self.fields:
            stderr.write("%s: %s\n" % (offset, field.name))
            offset += field.size

    def countInstances(self, instances):
        if self.structureName == "CHAR":
            if instances == None:
                return 0
            return len(instances)+1 # +1 terminating null character
        elif hasattr(instances,"__len__"):# either a list or an array of bytes
            return len(instances) 
        else: 
            raise Exception("Can't measure the length of %s which is a %s" % (instances, self.structureName))
    
    def validateInstance(self, instance, instanceName):
        for field in self.fields:
            try:
                fieldContent = getattr(instance, field.name)
            except AttributeError:
                raise Exception("%s does not have a field called %s%n" % (instanceName, field.name))
                raise

    def hasField(self, fieldName):
        return fieldName in self.nameToFieldMap

    def instancesToBytes(self, instances):
        if self.structureName == "CHAR":
            if type(instances) != str:
                raise Exception("Expected a string but it was a %s" % type(instances))
            return instances.encode("ASCII") + b'\x00'
        elif self.structureName == "U8__":
            if type(instances) != bytes and type(instances) != bytearray:
                raise Exception("Expected a byte array but it was a %s" % type(instances))
            return instances
        else:
            rawBytes = bytearray(self.size * len(instances))
            offset = 0
            
            if self.isPrimitive:
                structFormat = self.fields[0].structFormat
                for value in instances:
                    structFormat.pack_into(rawBytes, offset,value)
                    offset += self.size
            else:
                for value in instances:
                    value.writeToBuffer(rawBytes, offset)
                    offset += self.size
            return rawBytes
    
    def countBytesRequiredForInstances(self, instances):
        if self.structureName == "CHAR":
            return len(instances) + 1 # +1 for terminating character
        return self.size * self.countInstances(instances)

    


class M3Structure:
    
    def __init__(self, structureDescription, buffer=None, offset=0, checkExpectedValue=True):
        self.structureDescription = structureDescription

        if buffer != None:
            self.readFromBuffer(buffer, offset, checkExpectedValue)
        else:
            for field in self.structureDescription.fields:
                field.setToDefault(self)
            
        
        
    def introduceIndexReferences(self, indexMaker):
        for field in self.structureDescription.fields:
            field.introduceIndexReferences(self, indexMaker)
    
    def resolveReferences(self, sections):
        for field in self.structureDescription.fields:
            field.resolveIndexReferences(self, sections)
        
    def readFromBuffer(self, buffer, offset, checkExpectedValue):
        fieldOffset = offset
        for field in self.structureDescription.fields:
            field.readFromBuffer(self, buffer, fieldOffset, checkExpectedValue)
            fieldOffset += field.size
        assert fieldOffset - offset == self.structureDescription.size
    
    def writeToBuffer(self, buffer, offset):
        fieldOffset = offset
        for field in self.structureDescription.fields:
            field.writeToBuffer(self, buffer, fieldOffset)
            fieldOffset += field.size
        assert fieldOffset - offset == self.structureDescription.size
        
    def __str__(self):
        fieldValueMap = {}
        for field in self.structureDescription.fields:
            fieldValueMap[field.name] = str(getattr(self, field.name))
        return "%sV%s: {%s}" % (self.structureDescription.structureName, self.structureDescription.structureVersion, fieldValueMap )
    
    def getNamedBit(self, fieldName, bitName):
        field = self.structureDescription.nameToFieldMap[fieldName]
        return field.getNamedBit(self, bitName)
    
    def setNamedBit(self, fieldName, bitName, value):
        field = self.structureDescription.nameToFieldMap[fieldName]
        return field.setNamedBit(self, bitName, value)
    
    def getBitNameMaskPairs(self, fieldName):
        field = self.structureDescription.nameToFieldMap[fieldName]
        return field.getBitNameMaskPairs()
     
     
     
class Field:
    def __init__(self, name, sinceVersion, tillVersion):
        self.name = name
        self.sinceVersion = sinceVersion
        self.tillVersion = tillVersion

    def introduceIndexReferences(self, owner, indexMaker):
        pass

    def resolveIndexReferences(self, owner, sections):
        pass


class TagField(Field):

    def __init__(self, name, sinceVersion, tillVersion):
        Field.__init__(self, name, sinceVersion, tillVersion)
        self.structFormat = struct.Struct("<4B")
        self.size = 4
    
    def readFromBuffer(self, owner, buffer, offset, checkExpectedValue):
        b = self.structFormat.unpack_from(buffer, offset)
        if b[3] == 0:
            s = chr(b[2]) + chr(b[1]) + chr(b[0])
        else:
            s = chr(b[3]) + chr(b[2]) + chr(b[1]) + chr(b[0])
        
        setattr(owner, self.name, s)
    
    def writeToBuffer(self, owner, buffer, offset):
        s = getattr(owner, self.name)
        if len(s) == 4:
            b = (s[3] + s[2] + s[1] + s[0]).encode("ascii")
        else:
            b = (s[2] + s[1] + s[0]).encode("ascii") + b"\x00"
        return self.structFormat.pack_into(buffer, offset, b[0], b[1], b[2], b[3])

    def setToDefault(self, owner):
        pass
    
    def validateContent(self, fieldContent, fieldPath):
        if (type(fieldContent) != str) or (len(fieldContent) != 4):
            raise Exception("%s is not a string with 4 characters" % (fieldPath))

class ReferenceField(Field):
    def __init__(self, name, referenceStructureDescription, historyOfReferencedStructures, sinceVersion, tillVersion):
        Field.__init__(self, name, sinceVersion, tillVersion)
        self.referenceStructureDescription = referenceStructureDescription
        self.historyOfReferencedStructures = historyOfReferencedStructures
        self.size = referenceStructureDescription.size

    def introduceIndexReferences(self, owner, indexMaker):
        referencedObjects = getattr(owner, self.name)
        structureDescription = self.getListContentStructureDefinition(referencedObjects, "while adding index ref")
        
        indexReference = indexMaker.getIndexReferenceTo(referencedObjects, self.referenceStructureDescription, structureDescription)
        isPrimitive = self.historyOfReferencedStructures != None and self.historyOfReferencedStructures.isPrimitive 
        if not isPrimitive and len(referencedObjects) > 0:
            for referencedObject in referencedObjects:
                referencedObject.introduceIndexReferences(indexMaker)
        setattr(owner, self.name, indexReference)

    def resolveIndexReferences(self, owner, sections):
        ref = getattr(owner, self.name)
        ownerName = owner.structureDescription.structureName
        variable = "%(ownerName)s.%(fieldName)s" % {"ownerName":ownerName, "fieldName":self.name}
        
         
        if ref.entries == 0:
            if self.historyOfReferencedStructures == None:
                referencedObjects = []
            else:
                referencedObjects =  self.historyOfReferencedStructures.createEmptyArray()
        else:
            referencedSection = sections[ref.index]
            referencedSection.timesReferenced += 1
            indexEntry = referencedSection.indexEntry
            
            if indexEntry.repetitions < ref.entries:
                raise Exception("%s references more elements then there actually are" % variable)

            referencedObjects = referencedSection.content
            if self.historyOfReferencedStructures != None:
                expectedTagName = self.historyOfReferencedStructures.name
                actualTagName = indexEntry.tag
                if actualTagName != expectedTagName:
                    raise Exception("Expected ref %s point to %s, but it points to %s" % (variable, expectedTagName, actualTagName))
            else:
                raise Exception("Field %s can be marked as a reference pointing to %s" % (variable, indexEntry.tag))
        
        setattr(owner, self.name, referencedObjects)
    

    def getListContentStructureDefinition(self, l, contextString):

        if self.historyOfReferencedStructures == None:
            if len(l) == 0:
                return None
            else:
                ownerName = owner.structureDescription.structureName
                variable = "%(fieldName)s" % {"fieldName":self.name}
                raise Exception("%s: %s must be an empty list but wasn't" % (contextString,variable))
        if self.historyOfReferencedStructures.isPrimitive:
            return self.historyOfReferencedStructures.getVersion(0)
        
        if type(l) != list:
            raise Exception("%s: Expected a list, but was a %s" % (contextString,type(l)))
        if len(l) == 0:
            return None
        
        firstElement = l[0]
        contentClass = type(firstElement)
        if contentClass != M3Structure:
            raise Exception("%s: Expected a list to contain an M3Structure object and not a %s" % (contextString, contentClass))
        # Optional: Enable check:
        #if not contentClass.tagName == tagName:
        #    raise Exception("Expected a list to contain a object of a class with tagName %s, but it contained a object of class %s with tagName %s" % (tagName, contentClass, contentClass.tagName))
        return firstElement.structureDescription

    def readFromBuffer(self, owner, buffer, offset, checkExpectedValue):
        referenceObject = self.referenceStructureDescription.createInstance(buffer, offset, checkExpectedValue)
        setattr(owner, self.name, referenceObject)
 
    def writeToBuffer(self, owner, buffer, offset):
        referenceObject = getattr(owner, self.name)
        referenceObject.writeToBuffer(buffer, offset)

    def setToDefault(self, owner):
        
        if self.historyOfReferencedStructures != None:
            defaultValue = self.historyOfReferencedStructures.createEmptyArray()
        else:
            defaultValue = []
        setattr(owner, self.name, defaultValue )

    # The method validateContent is defined in subclasses

class CharReferenceField(ReferenceField):

    def __init__(self, name, referenceStructureDescription, historyOfReferencedStructures, sinceVersion, tillVersion):
        ReferenceField.__init__(self, name, referenceStructureDescription, historyOfReferencedStructures, sinceVersion, tillVersion)
    
    def validateContent(self, fieldContent, fieldPath):
        if (fieldContent != None) and (type(fieldContent) != str):
            raise Exception("%s is not a string but a %s" % (fieldPath, type(fieldContent)))

    
    
class ByteReferenceField(ReferenceField):
    
    def __init__(self, name, referenceStructureDescription, historyOfReferencedStructures, sinceVersion, tillVersion):
        ReferenceField.__init__(self, name, referenceStructureDescription, historyOfReferencedStructures, sinceVersion, tillVersion)

    def validateContent(self, fieldContent, fieldPath):
        if (type(fieldContent) != bytearray):
            raise Exception("%s is not a bytearray but a %s" % (fieldPath, type(fieldContent)))


class RealReferenceField(ReferenceField):
    
    def __init__(self, name, referenceStructureDescription, historyOfReferencedStructures, sinceVersion, tillVersion):
        ReferenceField.__init__(self, name, referenceStructureDescription, historyOfReferencedStructures, sinceVersion, tillVersion)

    def validateContent(self, fieldContent, fieldPath):
        if (type(fieldContent) != list):\
            raise Exception("%s is not a list of float" % (fieldPath))
        for itemIndex, item in enumerate(fieldContent):
            if type(item) != float: 
                itemPath = "%s[%d]" % (fieldPath, itemIndex)
                raise Exception("%s is not an float" % (itemPath))

    
class IntReferenceField(ReferenceField):
    intRefToMinValue = {"I16_":(-(1<<15)), "U16_":0, "I32_":(-(1<<31)), "U32_":0}
    intRefToMaxValue = {"I16_":((1<<15)-1), "U16_":((1<<16)-1), "I32_":((1<<31)-1), "U32_":((1<<32)-1), "FLAG":1}

    def __init__(self, name, referenceStructureDescription, historyOfReferencedStructures, sinceVersion, tillVersion):
        ReferenceField.__init__(self, name, referenceStructureDescription, historyOfReferencedStructures, sinceVersion, tillVersion)
        self.minValue = IntReferenceField.intRefToMinValue[historyOfReferencedStructures.name]
        self.maxValue = IntReferenceField.intRefToMaxValue[historyOfReferencedStructures.name]
        
    def validateContent(self, fieldContent, fieldPath):
        if (type(fieldContent) != list):
            raise Exception("%s is not a list of integers" % (fieldPath))
        for itemIndex, item in enumerate(fieldContent):
            itemPath = "%s[%d]" % (fieldPath, itemIndex)
            if type(item) != int: 
                raise Exception("%s is not an integer" % (itemPath))
            if (item < self.minValue) or (item > self.maxValue):
                raise Exception("%s has value %d which is not in range [%s, %s]"  % (itemPath, item, self.minValue, self.maxValue))


class StructureReferenceField(ReferenceField):
    
    def __init__(self, name, referenceStructureDescription, historyOfReferencedStructures, sinceVersion, tillVersion):
        ReferenceField.__init__(self, name, referenceStructureDescription, historyOfReferencedStructures, sinceVersion, tillVersion)

    def validateContent(self, fieldContent, fieldPath):
        if (type(fieldContent) != list):
            raise Exception("%s is not a list, but a %s" % (fieldPath, type(fieldContent)))
        if len(fieldContent) > 0:
            structureDescription = self.getListContentStructureDefinition(fieldContent, fieldPath)
            if structureDescription.history != self.historyOfReferencedStructures:
                raise Exception("Expected that %s is a list of %s and not %s" % (fieldPath, self.historyOfReferencedStructures.name, structureDescription.history.name))
            for itemIndex, item in enumerate(fieldContent):
                structureDescription.validateInstance(item, "%s[%d]" % (fieldPath, itemIndex))

class UnknownReferenceField(ReferenceField):
    
    def __init__(self, name, referenceStructureDescription, historyOfReferencedStructures, sinceVersion, tillVersion):
        ReferenceField.__init__(self, name, referenceStructureDescription, historyOfReferencedStructures, sinceVersion, tillVersion)

    def validateContent(self, fieldContent, fieldPath):
        if (type(fieldContent) != list) or (len(fieldContent) != 0):
            raise Exception("%s is not an empty list" % (fieldPath))

class EmbeddedStructureField(Field):
    
    def __init__(self, name, structureDescription, sinceVersion, tillVersion):
        Field.__init__(self, name, sinceVersion, tillVersion)
        self.structureDescription = structureDescription
        self.size = structureDescription.size
        
    def introduceIndexReferences(self, owner, indexMaker):
        emeddedStructure = getattr(owner, self.name)
        emeddedStructure.introduceIndexReferences(indexMaker)

    def resolveIndexReferences(self, owner, sections):
        emeddedStructure = getattr(owner, self.name)
        emeddedStructure.resolveReferences(sections)

    def toBytes(self, owner):
        emeddedStructure = getattr(owner, self.name)
        return emeddedStructure.toBytes()
        
    def readFromBuffer(self, owner, buffer, offset, checkExpectedValue):
        
        referenceObject = self.structureDescription.createInstance(buffer, offset, checkExpectedValue)
        setattr(owner, self.name, referenceObject)
    
    def writeToBuffer(self, owner, buffer, offset):
        emeddedStructure = getattr(owner, self.name)
        emeddedStructure.writeToBuffer(buffer, offset)

    def setToDefault(self, owner):
        v = self.structureDescription.createInstance()
        setattr(owner, self.name, v)

    def validateContent(self, fieldContent, fieldPath):
        self.structureDescription.validateInstance(fieldContent,fieldPath)


class PrimitiveField(Field):
    """ Base class for IntField and FloatField """

    def __init__(self, name, typeString, sinceVersion, tillVersion, defaultValue, expectedValue):
        Field.__init__(self, name, sinceVersion, tillVersion)
        self.size = primitiveFieldTypeSizes[typeString]
        self.structFormat = struct.Struct("<" + primitiveFieldTypeFormats[typeString])
        self.typeString = typeString
        self.defaultValue = defaultValue
        self.expectedValue = expectedValue

    def readFromBuffer(self, owner, buffer, offset, checkExpectedValue):
        value = self.structFormat.unpack_from(buffer, offset)[0]
        if self.expectedValue != None and value != self.expectedValue:
            structureName = owner.structureDescription.structureName
            structureVersion = owner.structureDescription.structureVersion
            raise Exception("Expected that field %s of %s (V. %d) has always the value %s, but it was %s" % (self.name, structureName, structureVersion, self.expectedValue, value))
        setattr(owner, self.name, value)

    def writeToBuffer(self, owner, buffer, offset):
        value = getattr(owner, self.name)
        return self.structFormat.pack_into(buffer, offset, value)
    
    def setToDefault(self, owner):
        setattr(owner, self.name, self.defaultValue)

class IntField(PrimitiveField):
    intTypeToMinValue = {"int16":(-(1<<15)), "uint16":0, "int32":(-(1<<31)), "uint32":0, "int8":-(1<<7), "uint8": 0}
    intTypeToMaxValue = {"int16":((1<<15)-1), "uint16":((1<<16)-1), "int32":((1<<31)-1), "uint32":((1<<32)-1), "int8":((1<<7)-1), "uint8": ((1<<8)-1)}

    def __init__(self, name, typeString, sinceVersion, tillVersion, defaultValue, expectedValue, bitMaskMap):
        PrimitiveField.__init__(self, name, typeString, sinceVersion, tillVersion, defaultValue, expectedValue)
        self.minValue = IntField.intTypeToMinValue[typeString]
        self.maxValue = IntField.intTypeToMaxValue[typeString]
        self.bitMaskMap = bitMaskMap
        
    def validateContent(self, fieldContent, fieldPath):
        if (type(fieldContent) != int):
            raise Exception("%s is not an int but a %s!" % (fieldPath, type(fieldContent)))
        if (fieldContent < self.minValue) or (fieldContent > self.maxValue):
            raise Exception("%s has value %d which is not in range [%d, %d]"  % (fieldPath, fieldContent, self.minValue, self.maxValue))

    def getNamedBit(self, owner, bitName):
        mask = self.bitMaskMap[bitName]
        intValue = getattr(owner, self.name)
        return ((intValue & mask) != 0)
    
    def setNamedBit(self, owner, bitName, value):
        mask = self.bitMaskMap[bitName]
        intValue = getattr(owner, self.name)
        if value:
            setattr(owner, self.name, intValue | mask)
        else:
            if (intValue & mask) != 0:
                setattr(owner, self.name,  intValue ^ mask)
    
    def getBitNameMaskPairs(self):
        return self.bitMaskMap.items()

class FloatField(PrimitiveField):
    
    def __init__(self, name, typeString, sinceVersion, tillVersion, defaultValue, expectedValue):
        PrimitiveField.__init__(self, name, typeString, sinceVersion, tillVersion, defaultValue, expectedValue)

    def validateContent(self, fieldContent, fieldPath):
        if (type(fieldContent) != float):
            raise Exception("%s is not a float but a %s!" % (fieldPath, type(fieldContent)))

class Fixed8Field(PrimitiveField):
    
    def __init__(self, name, typeString, sinceVersion, tillVersion, defaultValue, expectedValue):
        PrimitiveField.__init__(self, name, typeString, sinceVersion, tillVersion, defaultValue, expectedValue)

    def readFromBuffer(self, owner, buffer, offset, checkExpectedValue):
        intValue = self.structFormat.unpack_from(buffer, offset)[0]
        floatValue =  ((intValue / 255.0 * 2.0) -1) 
        
        if checkExpectedValue and self.expectedValue != None and floatValue != self.expectedValue:
            structureName = owner.structureDescription.structureName
            structureVersion = owner.structureDescription.structureVersion
            raise Exception("Expected that field %s of %s (V. %d) has always the value %s, but it was %s" % (self.name, structureName, structureVersion, self.expectedValue, intValue))
        setattr(owner, self.name, floatValue)

    def writeToBuffer(self, owner, buffer, offset):
        floatValue = getattr(owner, self.name)
        intValue = round((floatValue+1) / 2.0 * 255.0)
        return self.structFormat.pack_into(buffer, offset, intValue)
    
    
    def validateContent(self, fieldContent, fieldPath):
        if (type(fieldContent) != float):
            raise Exception("%s is not a float but a %s!" % (fieldPath, type(fieldContent)))
        
    
class UnknownBytesField(Field):

    def __init__(self, name, size, sinceVersion, tillVersion, defaultValue, expectedValue):
        Field.__init__(self, name, sinceVersion, tillVersion)
        self.size = size
        self.structFormat = struct.Struct("<%ss" % size)
        self.defaultValue = defaultValue
        self.expectedValue = expectedValue
        assert self.structFormat.size == self.size

    def readFromBuffer(self, owner, buffer, offset, checkExpectedValue):
        value = self.structFormat.unpack_from(buffer, offset)[0]
        if checkExpectedValue and self.expectedValue != None and value != self.expectedValue:
            raise Exception("Expected that %sV%s.%s has always the value %s, but it was %s" % (owner.structureDescription.structureName, owner.structureDescription.structureVersion, self.name, self.expectedValue, value))

        setattr(owner, self.name, value)
    
    def writeToBuffer(self, owner, buffer, offset):
        value = getattr(owner, self.name)
        return self.structFormat.pack_into(buffer, offset, value)

    def setToDefault(self, owner):
        setattr(owner, self.name, self.defaultValue)

    def validateContent(self, fieldContent, fieldPath):
        if (type(fieldContent) != bytes) or (len(fieldContent) != self.size):
            raise Exception("%s is not an bytes object of size %s" % (fieldPath, self.size))


class Visitor:
    def visitStart(self, generalDataMap):
        pass
    def visitClassStart(self, generalDataMap, classDataMap):
        pass
    def visitVersion(self, generalDataMap, classDataMap, versionDataMap):
        pass
    def visitFieldStart(self, generalDataMap, classDataMap, fieldDataMap):
        pass
    def visitFieldBit(self, generalDataMap, classDataMap, fieldDataMap, bitDataMap):
        pass
    def visitFieldEnd(self, generalDataMap, classDataMap, fieldDataMap):
        pass
    def visitClassEnd(self, generalDataMap, classDataMap):
        pass
    def visitEnd(self, generalDataMap):
        pass

class StructureAttributesReader(Visitor):
    def visitClassStart(self, generalDataMap, classDataMap):
        xmlNode = classDataMap["xmlNode"]
        if xmlNode.hasAttribute("name"):
            classDataMap["structureName"] = xmlNode.getAttribute("name")
        else:
            raise Exception("There is a structure without a name attribute")
        classDataMap["versionToSizeMap"] = {}
        
    def visitVersion(self, generalDataMap, classDataMap, versionDataMap):
        xmlNode = versionDataMap["xmlNode"]
        if xmlNode.hasAttribute("number"):
            version = int(xmlNode.getAttribute("number"))
        else:
            raise Exception("The structure %s has a version element without a number attribute" % classDataMap["tagName"])
        if xmlNode.hasAttribute("size"):
            sizeString = xmlNode.getAttribute("size")
            try:
                size = int(sizeString)
            except ValueError:
                structureName = classDataMap["structureName"]
                raise Exception("The size specified for version %d of structure %s is not an int" % (version, structureName))
        else:
            raise Exception("The structure %s has a version element without a size attribute" % classDataMap["tagName"])
        versionToSizeMap = classDataMap["versionToSizeMap"]
        if version in versionToSizeMap:
            raise Exception(("The structure %s has two times the same version" % classDataMap["tagName"]))
        versionToSizeMap[version] = size


class StructureDescriptionReader(Visitor):
    def visitClassStart(self, generalDataMap, classDataMap):
        xmlNode = classDataMap["xmlNode"]
        tagDescriptionNodes = xmlNode.getElementsByTagName("description")
        if len(tagDescriptionNodes) != 1:
            raise Exception("Tag %s has not exactly one description node",fullName)
        tagDescriptionNode = tagDescriptionNodes[0]
        tagDescription = ""
        for descriptionChild in tagDescriptionNode.childNodes:
            if descriptionChild.nodeType == Node.TEXT_NODE:
                tagDescription += descriptionChild.data
        classDataMap["description"] = tagDescription
   

class FieldAttributesReader(Visitor):
    def visitFieldStart(self, generalDataMap, classDataMap, fieldDataMap):
        xmlNode = fieldDataMap["xmlNode"]
        if xmlNode.hasAttribute("name"):
             fieldDataMap["fieldName"] = xmlNode.getAttribute("name")
        else:
            fullName = classDataMap["fullName"]
            raise Exception("There is a field in %s without a name attribute" % fullName)
        
        if xmlNode.hasAttribute("type"):
            fieldDataMap["typeString"] = xmlNode.getAttribute("type")
        else:
            fieldDataMap["typeString"] = None
        
        if xmlNode.hasAttribute("refTo"):
            fieldDataMap["refTo"] = xmlNode.getAttribute("refTo")
        else:
            fieldDataMap["refTo"] = None

        if xmlNode.hasAttribute("size"):
            fieldDataMap["specifiedFieldSize"] = int(xmlNode.getAttribute("size"))
        else:
            fieldDataMap["specifiedFieldSize"] = None

        if xmlNode.hasAttribute("expected-value"):
            fieldDataMap["expectedValueString"] = xmlNode.getAttribute("expected-value")
        else:
            fieldDataMap["expectedValueString"] = None
            
        if xmlNode.hasAttribute("default-value"):
            fieldDataMap["defaultValueString"] = xmlNode.getAttribute("default-value")
        else:
            fieldDataMap["defaultValueString"] = None

        if xmlNode.hasAttribute("till-version"):
            fieldDataMap["tillVersion"] = int(xmlNode.getAttribute("till-version"))
        else:
            fieldDataMap["tillVersion"] = None
            
        if xmlNode.hasAttribute("since-version"):
            fieldDataMap["sinceVersion"] = int(xmlNode.getAttribute("since-version"))
        else:
            fieldDataMap["sinceVersion"] = None


class BitAttributesReader(Visitor):

    def visitFieldBit(self, generalDataMap, classDataMap, fieldDataMap, bitDataMap):
        xmlNode = bitDataMap["xmlNode"]
        if xmlNode.hasAttribute("name"):
             bitDataMap["name"] = xmlNode.getAttribute("name")
        else:
            structureName = classDataMap["structureName"]
            fieldName = fieldDataMap["fieldName"]
            raise Exception("There is bit xml node in field %(fieldName)s of structure %(structureName)s without a name attribute" % {"fieldName": fieldName,"structureName":structureName})
        
        if xmlNode.hasAttribute("mask"):
            maskString = xmlNode.getAttribute("mask")
            if not re.match("0x[0-9]+", maskString):
                structureName = classDataMap["structureName"]
                fieldName = fieldDataMap["fieldName"]
                bitName = bitDataMap["name"]
                raise Exception("The bit %(bitName)s of %(structureName)s.%(fieldName)s has an invalid mask attribute" % {"fieldName": fieldName,"structureName":structureName,"bitName":bitName})

            bitDataMap["mask"] = int(maskString,0)
        else:
            structureName = classDataMap["structureName"]
            fieldName = fieldDataMap["fieldName"]
            raise Exception("There is bit xml node in field %(fieldName)s of structure %(structureName)s without a mask attribute" % {"fieldName": fieldName,"structureName":structureName})

class ExpectedAndDefaultConstantsDeterminer(Visitor):
    def parseHex(self, hexString):
        hexString = hexString[2:]
        return bytes([int(hexString[x:x+2], 16) for x in range(0, len(hexString),2)])
        
    def visitFieldStart(self, generalDataMap, classDataMap, fieldDataMap):
        structureName = classDataMap["structureName"]
        fieldName = fieldDataMap["fieldName"]
        fieldType = fieldDataMap["typeString"]
        refTo = fieldDataMap["refTo"]
        expectedValueString = fieldDataMap["expectedValueString"]
        defaultValueString = fieldDataMap["defaultValueString"]
        variableName = "%s.%s" % (structureName, fieldName)
        structures = generalDataMap["structures"]
        expectedValue = None
        defaultValue = None
        if fieldType in ("int32", "int16", "int8", "uint8","uint16", "uint32"):
            if expectedValueString != None:
                try:
                    expectedValue = int(expectedValueString, 0)
                except ValueError:
                    raise Exception("The specified expected value for %s is not an integer" % variableName)
            
            if defaultValueString != None:
                try:
                    defaultValue = int(defaultValueString, 0)
                except ValueError:
                    raise Exception("The specified default value for %s is not an integer" % variableName)
            else:
                if expectedValue != None:
                    defaultValue = expectedValue
                else:
                    defaultValue = 0
        elif fieldType == "float" or fieldType == "fixed8":
            if expectedValueString != None:
                try:
                    expectedValue = float(expectedValueString)
                except ValueError:
                    raise Exception("The specified expected value for %s is not a float" % variableName)

            if defaultValueString != None:
                try:
                    defaultValue = float(defaultValueString)
                except ValueError:
                    raise Exception("The specified default value for %s is not a a float" % variableName)
            else:
                if expectedValue != None:
                    defaultValue = expectedValue
                else:
                    defaultValue = 0.0
        elif fieldType == None:
            specifiedFieldSize = fieldDataMap["specifiedFieldSize"]
            defaultValue = None
            if expectedValueString != None:
                if not expectedValueString.startswith("0x"):
                    raise Exception('The expected-value "%s" of field %s does not start with 0x' % (hexString, variableName) )
                expectedValue = self.parseHex(expectedValueString)
                defaultValue = expectedValue
            
            if defaultValueString != None:
                if not defaultValueString.startswith("0x"):
                    raise Exception('The expected-value "%s" of field %s does not start with 0x' % (hexString, variableName) )
                defaultValue = self.parseHex(defaultValueString)

            if defaultValue == None:
                defaultValue = bytes(specifiedFieldSize)

        fieldDataMap["expectedValue"] = expectedValue
        fieldDataMap["defaultValue"] = defaultValue

class BitMaskMapDeterminer(Visitor):
    def visitFieldStart(self, generalDataMap, classDataMap, fieldDataMap):
        fieldDataMap["bitMaskMap"] = {}
        
    def visitFieldBit(self, generalDataMap, classDataMap, fieldDataMap, bitDataMap):
        bitName = bitDataMap["name"]
        bitMask = bitDataMap["mask"]
        bitMaskMap = fieldDataMap["bitMaskMap"]
        bitMaskMap[bitName] = bitMask
        

class FieldListCreator(Visitor):
    def visitClassStart(self, generalDataMap, classDataMap):
        classDataMap["fields"] = []

    def visitFieldEnd(self, generalDataMap, classDataMap, fieldDataMap):
        fields = classDataMap["fields"]
        structureName = classDataMap["structureName"]
        fieldName = fieldDataMap["fieldName"]
        typeString = fieldDataMap["typeString"] 
        sinceVersion = fieldDataMap["sinceVersion"] 
        tillVersion = fieldDataMap["tillVersion"] 
        defaultValue = fieldDataMap["defaultValue"] 
        expectedValue = fieldDataMap["expectedValue"] 
        specifiedFieldSize = fieldDataMap["specifiedFieldSize"]
        structures = generalDataMap["structures"]
        bitMaskMap = fieldDataMap["bitMaskMap"]

        #TODO validate field size
        if typeString == "tag":
            field = TagField(fieldName, sinceVersion, tillVersion)
        elif typeString in intTypes:
            field = IntField(fieldName, typeString, sinceVersion, tillVersion, defaultValue, expectedValue, bitMaskMap)
        elif typeString == "float":
            field = FloatField(fieldName, typeString, sinceVersion, tillVersion, defaultValue, expectedValue)
        elif typeString == "fixed8":
            field = Fixed8Field(fieldName, typeString, sinceVersion, tillVersion, defaultValue, expectedValue)
        elif typeString == None:
            field = UnknownBytesField(fieldName, specifiedFieldSize, sinceVersion, tillVersion, defaultValue, expectedValue)
        else:
            vPos = typeString.rfind("V")
            if vPos != -1:
                fieldStructureName = typeString[:vPos]
                fieldStructureVersion = int(typeString[vPos+1:])

            else:
                fieldStructureName = typeString
                fieldStructureVersion = 0
            if fieldStructureName == "Reference" or fieldStructureName == "SmallReference":
                refTo = fieldDataMap["refTo"]
                if (refTo != None) and (not (refTo in structures)):
                    raise Exception("The structure with name %s referenced by %s.%s is not defined" % (refTo, structureName, fieldName))
                if refTo != None:
                    historyOfReferencedStructures = structures[refTo]
                else:
                    historyOfReferencedStructures = None
                referenceStructureDescription = structures[fieldStructureName].getVersion(fieldStructureVersion)
                
                if refTo == None:
                    field = UnknownReferenceField(fieldName, referenceStructureDescription, historyOfReferencedStructures, sinceVersion, tillVersion)
                elif refTo == "CHAR":
                    field = CharReferenceField(fieldName, referenceStructureDescription, historyOfReferencedStructures, sinceVersion, tillVersion)
                elif refTo == "U8__":
                    field = ByteReferenceField(fieldName, referenceStructureDescription, historyOfReferencedStructures, sinceVersion, tillVersion)
                elif refTo == "REAL":
                    field = RealReferenceField(fieldName, referenceStructureDescription, historyOfReferencedStructures, sinceVersion, tillVersion)
                elif refTo in  ["I16_", "U16_", "I32_", "U32_"]:
                    field = IntReferenceField(fieldName, referenceStructureDescription, historyOfReferencedStructures, sinceVersion, tillVersion)
                else:
                    field = StructureReferenceField(fieldName, referenceStructureDescription, historyOfReferencedStructures, sinceVersion, tillVersion)
            else:
                fieldStructureHistory = structures.get(fieldStructureName)
                if fieldStructureHistory == None:
                    raise Exception("The structure %s has not been defined before structure %s" % (fieldStructureName, structureName))
                fieldStructureDescription = fieldStructureHistory.getVersion(fieldStructureVersion)
                field = EmbeddedStructureField(fieldName, fieldStructureDescription, sinceVersion, tillVersion)
        fields.append(field)

class StructureHistoryListCreator(Visitor):
    def visitStart(self, generalDataMap):
        generalDataMap["structures"] = {}
        pass
    
    def visitClassEnd(self, generalDataMap, classDataMap):
        fields = classDataMap["fields"]
        structureName = classDataMap["structureName"]
        versionToSizeMap = classDataMap["versionToSizeMap"]
        structures = generalDataMap["structures"]
        
        structureHistory = M3StructureHistory(structureName, versionToSizeMap, fields)
        structures[structureName] = structureHistory
    def visitEnd(self, generalDataMap):
        pass



 

def foreachChildWithName(parentNode, childName):
    for childNode in parentNode.childNodes:
        if childNode.nodeName == childName:
            yield childNode

def firstNodeWithName(parentNode, childName):
    for childNode in parentNode.childNodes:
        if childNode.nodeName == childName:
            return childNode
    return None
    
    
def visitStructresDomWith(structuresDom, visitors, generalDataMap):
    for visitor in visitors:
        visitor.visitStart(generalDataMap)

    structuresNode = structuresDom.documentElement
    for structureNode in foreachChildWithName(structuresNode, "structure"):
        classDataMap = {}
        classDataMap["xmlNode"] = structureNode

        for visitor in visitors:
            visitor.visitClassStart(generalDataMap, classDataMap)

        versionsNode = firstNodeWithName(structureNode, "versions")
        for versionNode in foreachChildWithName(versionsNode, "version"):
            versionDataMap = {}
            versionDataMap["xmlNode"] = versionNode
            for visitor in visitors:

                visitor.visitVersion(generalDataMap, classDataMap, versionDataMap)
        
        fieldsNode = firstNodeWithName(structureNode, "fields")
        for fieldNode in foreachChildWithName(fieldsNode, "field"):
            fieldDataMap = {}
            fieldDataMap["xmlNode"] = fieldNode
            for visitor in visitors:
                visitor.visitFieldStart(generalDataMap, classDataMap, fieldDataMap)
            
            bitsNode = firstNodeWithName(fieldNode, "bits")
            if bitsNode != None:
                for bitNode in foreachChildWithName(bitsNode, "bit"):
                    bitDataMap = {}
                    bitDataMap["xmlNode"] = bitNode
                    
                    for visitor in visitors:
                        visitor.visitFieldBit(generalDataMap, classDataMap, fieldDataMap, bitDataMap)

            for visitor in visitors:
                visitor.visitFieldEnd(generalDataMap, classDataMap, fieldDataMap)

        for visitor in visitors:
            visitor. visitClassEnd(generalDataMap, classDataMap)

    for visitor in visitors:
        visitor.visitEnd(generalDataMap)

def readStructureDefinitions(structuresXmlFile):
    doc = xml.dom.minidom.parse(structuresXmlFile)
    generalDataMap = {}

    # first run is only for determing the complete list of known structures
    #firstRunVisitors = [
    #    StructureAttributesReader(),
        #KnownStructuresListDeterminer(),
        #KnownTagsListDeterminer()
    #    ]
    #visitStructresDomWith(doc, firstRunVisitors, generalDataMap)

    secondRunVisitors = [
        StructureAttributesReader(),
        StructureDescriptionReader(),
        FieldAttributesReader(),
        ExpectedAndDefaultConstantsDeterminer(),
        BitAttributesReader(),
        BitMaskMapDeterminer(),
        FieldListCreator(),
        StructureHistoryListCreator()
        ] 
    #FieldIndexDeterminer(),
    #BitAttributesReader()
    #DuplicateFieldNameChecker(), 
    s = """
    SizeDeterminer(),
    ClassHeaderAdder(),
    FullNameConstantAdder(),
    TagNameConstantAdder(),
    VersionConstantAdder(),
    StructSizeConstantAdder(),
    StructFormatConstantAdder(),
    FieldsConstantAdder(), 
    SetAttributesMethodAdder(), 
    ToStringMethodAdder(), 
    ReferenceFeatureAdder(),
    ExpectedAndDefaultConstantsDeterminer(),
    CreateInstancesFeatureAdder(),
    ToBytesFeatureAdder(),
    BitMethodsAdder(),
    GetFieldTypeInfoMethodAdder(),
    ValidateMethodAdder(),
    StructureMapAdder(),
    FooterAdder()]"""
        
    visitStructresDomWith(doc, secondRunVisitors, generalDataMap)

    return generalDataMap["structures"]



def resolveAllReferences(list, sections):
    ListType = type([])
    for sublist in list:
        if type(sublist) == ListType:
            for entry in sublist:
                entry.resolveReferences(sections)

def loadSections(filename, checkExpectedValue=True):
    source = open(filename, "rb")
    try:
        MD34V11 = structures["MD34"].getVersion(11)
        headerBytes = source.read(MD34V11.size)
        header = MD34V11.createInstance(headerBytes, checkExpectedValue=checkExpectedValue)
        
        source.seek(header.indexOffset)
        MD34IndexEntryV0 = structures["MD34IndexEntry"].getVersion(0)
        sections = []
        for i in range(header.indexSize):
            section = Section()
            indexEntryBytes = source.read(MD34IndexEntryV0.size)
            section.indexEntry = MD34IndexEntryV0.createInstance(indexEntryBytes, checkExpectedValue=checkExpectedValue)
            sections.append(section)
        
        offsets = []
        for section in sections:
            indexEntry = section.indexEntry
            offsets.append(indexEntry.offset)
        offsets.append(header.indexOffset)
        offsets.sort()
        previousOffset = offsets[0]
        offsetToSizeMap = {}
        for offset in offsets[1:]:
            offsetToSizeMap[previousOffset] = offset - previousOffset
            previousOffset = offset
        
        unknownSections = set()
        for section in sections:
            indexEntry = section.indexEntry
            source.seek(indexEntry.offset)
            numberOfBytes = offsetToSizeMap[indexEntry.offset]
            section.rawBytes = source.read(numberOfBytes)
            
            structureHistory = structures.get(indexEntry.tag)
            if structureHistory != None:
                structureDescription = structureHistory.getVersion(indexEntry.version)
            else:
                structureDescription = None

            if structureDescription != None:
                section.structureDescription = structureDescription
                section.determineContentField(checkExpectedValue)
            else:
                guessedUnusedSectionBytes = 0
                for i in range (1,16):
                    if section.rawBytes[len(section.rawBytes)-i] == 0xaa:
                        guessedUnusedSectionBytes += 1
                    else:
                        break
                guessedBytesPerEntry = float(len(section.rawBytes) - guessedUnusedSectionBytes) / indexEntry.repetitions
                message = "ERROR: Unknown section at offset %s with tag=%s version=%s repetitions=%s sectionLengthInBytes=%s guessedUnusedSectionBytes=%s guessedBytesPerEntry=%s\n" % (indexEntry.offset, indexEntry.tag, indexEntry.version, indexEntry.repetitions, len(section.rawBytes),guessedUnusedSectionBytes,guessedBytesPerEntry )
                stderr.write(message)
                unknownSections.add("%sV%s" % (indexEntry.tag, indexEntry.version))
        if len(unknownSections) != 0:
            raise Exception("There were %s unknown sections: %s (see console log for more details)" % (len(unknownSections), unknownSections))
    finally:
        source.close()
    return sections

def resolveReferencesOfSections(sections):
    for section in sections:
        section.resolveReferences(sections)

def checkThatAllSectionsGotReferenced(sections):
    numberOfUnreferencedSections = 0
    referenceStructureDescription = reference = structures["Reference"].getVersion(0)
    for sectionIndex, section in enumerate(sections):
        
        if (section.timesReferenced == 0) and (sectionIndex != 0):
            numberOfUnreferencedSections += 1
            stderr.write("WARNING: %sV%s (%d repetitions) got %d times referenced\n" % (section.indexEntry.tag, section.indexEntry.version, section.indexEntry.repetitions , section.timesReferenced))
            reference = referenceStructureDescription.createInstance()
            reference.entries = section.indexEntry.repetitions
            reference.index = sectionIndex
            reference.flags = 0
            bytesToSearch = referenceStructureDescription.instancesToBytes([reference])
            possibleReferences = 0
            for sectionToCheck in sections:
                positionInSection = sectionToCheck.rawBytes.find(bytesToSearch)
                if positionInSection != -1:
                    possibleReferences += 1
                    stderr.write("  -> Found a reference at offset %d in a section of type %sV%s\n" % (positionInSection, sectionToCheck.indexEntry.tag,sectionToCheck.indexEntry.version)) 
                    sectionToCheck.structureDescription.dumpOffsets()

            if possibleReferences == 0:
                bytesToSearch = bytesToSearch[0:-4]
                for sectionToCheck in sections:
                    positionInSection = sectionToCheck.rawBytes.find(bytesToSearch)
                    if positionInSection != -1:
                        flagBytes = sectionToCheck.rawBytes[positionInSection+8:positionInSection+12]
                        flagsAsHex = byteDataToHex(flagBytes)
                        stderr.write("  -> Found maybe a reference at offset %d in a section of type %sV%s with flag %s\n" % (positionInSection, sectionToCheck.indexEntry.tag,sectionToCheck.indexEntry.version, flagsAsHex)) 
                        sectionToCheck.structureDescription.dumpOffsets()

    if numberOfUnreferencedSections > 0:
        raise Exception("Unable to load all data: There were %d unreferenced sections. View log for details" % numberOfUnreferencedSections)

def loadModel(filename, checkExpectedValue=True):
    sections = loadSections(filename, checkExpectedValue)
    resolveReferencesOfSections(sections)
    checkThatAllSectionsGotReferenced(sections)
    header = sections[0].content[0]
    model = header.model[0]
    modelDescription = model.structureDescription
    modelDescription.validateInstance(model, "model")
    return model

class IndexReferenceSourceAndSectionListMaker:
    """ Creates a list of sections which are needed to store the objects for which index references are requested"""
    def __init__(self):
        self.objectsIdToIndexReferenceMap = {}
        self.offset = 0
        self.nextFreeIndexPosition = 0
        self.sections = []
        self.MD34IndexEntry = structures["MD34IndexEntry"].getVersion(0)
    
    def getIndexReferenceTo(self, objectsToSave, referenceStructureDescription, structureDescription):
        if id(objectsToSave) in self.objectsIdToIndexReferenceMap.keys():
            return self.objectsIdToIndexReferenceMap[id(objectsToSave)]

        if structureDescription == None:
            repetitions = 0
        else:
            repetitions = structureDescription.countInstances(objectsToSave)
        
        indexReference = referenceStructureDescription.createInstance()
        indexReference.entries = repetitions
        indexReference.index = self.nextFreeIndexPosition
        
        if (repetitions > 0):
            indexEntry = self.MD34IndexEntry.createInstance()
            indexEntry.tag = structureDescription.structureName
            indexEntry.offset = self.offset
            indexEntry.repetitions = repetitions
            indexEntry.version = structureDescription.structureVersion
            
            section = Section()
            section.indexEntry = indexEntry
            section.content = objectsToSave
            section.structureDescription = structureDescription
            self.sections.append(section)
            self.objectsIdToIndexReferenceMap[id(objectsToSave)] = indexReference
            totalBytes = section.bytesRequiredForContent()
            totalBytes = increaseToValidSectionSize(totalBytes)
            self.offset += totalBytes
            self.nextFreeIndexPosition += 1
        return indexReference
    
    
def modelToSections(model):
    MD34V11 = structures["MD34"].getVersion(11)
    header = MD34V11.createInstance()
    header.tag = "MD34"
    header.model = [model]
    ReferenceV0 = structures["Reference"].getVersion(0)
    indexMaker = IndexReferenceSourceAndSectionListMaker()
    indexMaker.getIndexReferenceTo([header], ReferenceV0, MD34V11)
    header.introduceIndexReferences(indexMaker)
    sections = indexMaker.sections
    header.indexOffset = indexMaker.offset
    header.indexSize = len(sections)

    for section in sections:
        section.determineFieldRawBytes()
    return sections

def saveSections(sections, filename):
    fileObject = open(filename, "w+b")
    try:
        previousSection = None
        for section in sections:
            if section.indexEntry.offset != fileObject.tell():
                raise Exception("Section length problem: Section with index entry %(previousIndexEntry)s has length %(previousLength)s and gets followed by section with index entry %(currentIndexEntry)s" % {"previousIndexEntry":previousSection.indexEntry,"previousLength":len(previousSection.rawBytes),"currentIndexEntry":section.indexEntry} )
            fileObject.write(section.rawBytes)
            previousSection = section
        header = sections[0].content[0]
        if fileObject.tell() != header.indexOffset:
            raise Exception("Not at expected write position %s after writing sections, but %s"%(header.indexOffset, fileObject.tell()))
        for section in sections:
            indexEntryBytesBuffer = bytearray(section.indexEntry.structureDescription.size)
            section.indexEntry.writeToBuffer(indexEntryBytesBuffer, 0)
            fileObject.write(indexEntryBytesBuffer)
    finally:
        fileObject.close()
        
def saveAndInvalidateModel(model, filename):
    '''Do not use the model object after calling this method since it gets modified'''
    model.structureDescription.validateInstance(model,"model")
    sections = modelToSections(model)
    saveSections(sections, filename)

def readStructures():
    from os import path
    directory = path.dirname(__file__)
    structuresXmlPath = path.join(directory, "structures.xml")
    return readStructureDefinitions(structuresXmlPath)

structures = readStructures()
    