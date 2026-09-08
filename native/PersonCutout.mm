#import <Vision/Vision.h>
#import <CoreVideo/CoreVideo.h>
#include <cstring>

// Synchronous caller-owned input/output buffers; no camera, file or network access.
extern "C" void *TigaCreatePersonCutout()
{
    @autoreleasepool {
        if (@available(macOS 12.0, *)) {
            VNGeneratePersonSegmentationRequest *request = [VNGeneratePersonSegmentationRequest new];
            request.qualityLevel = VNGeneratePersonSegmentationRequestQualityLevelBalanced;
            request.outputPixelFormat = kCVPixelFormatType_OneComponent8;
            return (void *)CFBridgingRetain(request);
        }
        return nullptr;
    }
}

extern "C" void TigaDestroyPersonCutout(void *context)
{
    if (context) CFRelease(context);
}

extern "C" int TigaPersonMask(void *context, const unsigned char *bgra, int width, int height, unsigned char *output)
{
    if (!context || !bgra || !output || width <= 0 || height <= 0 || width > 640 || height > 480) return -1;
    @autoreleasepool {
        if (@available(macOS 12.0, *)) {
            CVPixelBufferRef input = nullptr;
            NSDictionary *attributes = @{(NSString *)kCVPixelBufferIOSurfacePropertiesKey: @{}};
            if (CVPixelBufferCreate(kCFAllocatorDefault, width, height, kCVPixelFormatType_32BGRA,
                                   (__bridge CFDictionaryRef)attributes, &input) != kCVReturnSuccess) return -2;
            CVPixelBufferLockBaseAddress(input, 0);
            auto *bytes = (unsigned char *)CVPixelBufferGetBaseAddress(input);
            size_t stride = CVPixelBufferGetBytesPerRow(input);
            for (int y = 0; y < height; ++y) memcpy(bytes + y * stride, bgra + y * width * 4, width * 4);
            CVPixelBufferUnlockBaseAddress(input, 0);
            VNGeneratePersonSegmentationRequest *request = (__bridge VNGeneratePersonSegmentationRequest *)context;
            VNImageRequestHandler *handler = [[VNImageRequestHandler alloc] initWithCVPixelBuffer:input options:@{}];
            NSError *error = nil;
            BOOL success = [handler performRequests:@[request] error:&error];
            CVPixelBufferRelease(input);
            if (!success || !request.results.count) return -3;
            CVPixelBufferRef mask = request.results.firstObject.pixelBuffer;
            CVPixelBufferLockBaseAddress(mask, kCVPixelBufferLock_ReadOnly);
            const auto *values = (const unsigned char *)CVPixelBufferGetBaseAddress(mask);
            size_t mw = CVPixelBufferGetWidth(mask), mh = CVPixelBufferGetHeight(mask), ms = CVPixelBufferGetBytesPerRow(mask);
            for (int y = 0; y < height; ++y) for (int x = 0; x < width; ++x)
                output[y * width + x] = values[(y * mh / height) * ms + x * mw / width];
            CVPixelBufferUnlockBaseAddress(mask, kCVPixelBufferLock_ReadOnly);
            return 0;
        }
        return -4;
    }
}
