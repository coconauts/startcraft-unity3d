//
//  AMPConstants.m

#import "AMPConstants.h"

NSString *const kAMPLibrary = @"amplitude-ios";
NSString *const kAMPPlatform = @"iOS";
NSString *const kAMPVersion = @"3.0.0";
NSString *const kAMPEventLogDomain = @"api.amplitude.com";
NSString *const kAMPEventLogUrl = @"https://api.amplitude.com/";
const int kAMPApiVersion = 2;
const int kAMPEventUploadThreshold = 30;
const int kAMPEventUploadMaxBatchSize = 100;
const int kAMPEventMaxCount = 1000;
const int kAMPEventRemoveBatchSize = 20;
const int kAMPEventUploadPeriodSeconds = 30; // 30s
const long kAMPMinTimeBetweenSessionsMillis = 5 * 60 * 1000; // 5m
