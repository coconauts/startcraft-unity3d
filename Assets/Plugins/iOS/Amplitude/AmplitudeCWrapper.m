#import "AmplitudeCWrapper.h"
#import "Amplitude.h"
#import "AMPARCMacros.h"


// Used to allocate a C string on the heap for C#
char* MakeCString(const char* string)
{
    if (string == NULL) {
        return NULL;
    }

    char* result = (char*) malloc(strlen(string) + 1);
    strcpy(result, string);

    return result;
}

// Converts C style string to NSString
NSString* ToNSString(const char* string)
{
	if (string)
		return [NSString stringWithUTF8String: string];
	else
		return nil;
}

NSDictionary* ToNSDictionary(const char* data)
{
    if (data) {
        NSError *error = nil;
        NSDictionary *result = [NSJSONSerialization JSONObjectWithData:[ToNSString(data) dataUsingEncoding:NSUTF8StringEncoding] options:0 error:&error];

        if (error != nil) {
            NSLog(@"ERROR: Deserialization error:%@", error);
            return nil;
        } else if (![result isKindOfClass:[NSDictionary class]]) {
            NSLog(@"ERROR: invalid type:%@", [result class]);
            return nil;
        } else {
            return result;
        }
    } else {
        return nil;
    }
}

void _Amplitude_init(const char* apiKey, const char* userId)
{
	if (userId) {
	    [[Amplitude instance] initializeApiKey:ToNSString(apiKey)
	                         userId:ToNSString(userId)];
	} else {
		[[Amplitude instance] initializeApiKey:ToNSString(apiKey)];
	}
}

void _Amplitude_logEvent(const char* event, const char* properties)
{
	if (properties) {
    	[[Amplitude instance] logEvent:ToNSString(event) withEventProperties:ToNSDictionary(properties)];
	} else {
		[[Amplitude instance] logEvent:ToNSString(event)];
	}
}

void _Amplitude_logOutOfSessionEvent(const char* event, const char* properties)
{
    if (properties) {
        [[Amplitude instance] logEvent:ToNSString(event) withEventProperties:ToNSDictionary(properties) outOfSession:true];
    } else {
        [[Amplitude instance] logEvent:ToNSString(event) withEventProperties:nil outOfSession:true];
    }
}

void _Amplitude_setUserId(const char* event)
{
	[[Amplitude instance] setUserId:ToNSString(event)];
}

void _Amplitude_setUserProperties(const char* properties)
{
	[[Amplitude instance] setUserProperties:ToNSDictionary(properties)];
}

void _Amplitude_setOptOut(const bool enabled)
{
    [[Amplitude instance] setOptOut:enabled];
}

void _Amplitude_logRevenueAmount(double amount)
{
	[[Amplitude instance] logRevenue:[NSNumber numberWithDouble:amount]];
}

void _Amplitude_logRevenue(const char* productIdentifier, int quantity, double price)
{
    [[Amplitude instance] logRevenue:ToNSString(productIdentifier) quantity:quantity price:[NSNumber numberWithDouble:price]];
}

void _Amplitude_logRevenueWithReceipt(const char* productIdentifier, int quantity, double price, const char* receipt)
{
    NSData *receiptData = [[NSData alloc] initWithBase64EncodedString:ToNSString(receipt) options:0];
    [[Amplitude instance] logRevenue:ToNSString(productIdentifier) quantity:quantity price:[NSNumber numberWithDouble:price] receipt:receiptData];
    SAFE_ARC_RELEASE(receiptData);
}

const char * _Amplitude_getDeviceId()
{
    return MakeCString([[[Amplitude instance] getDeviceId] UTF8String]);
}
