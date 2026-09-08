#import <Cocoa/Cocoa.h>
#include <cstring>
#include <mutex>
#include <string>

static std::mutex choiceMutex;
static std::string choicePath;
static int choiceState = 0;
static bool choicePending = false;

// Unity keeps running its event loop while Cocoa presents and services the panel.
extern "C" void TigaBeginMusicChoice()
{
    {
        std::lock_guard<std::mutex> lock(choiceMutex);
        if (choicePending) return;
        choicePending = true; choiceState = 0; choicePath.clear();
    }
    dispatch_async(dispatch_get_main_queue(), ^{
        NSOpenPanel *panel = [NSOpenPanel openPanel];
        panel.title = @"选择背景音乐";
        panel.message = @"选择 MP3、WAV、OGG 或 AIFF 音频。游戏只在本机播放。";
        panel.prompt = @"使用这首音乐";
        panel.canChooseFiles = YES; panel.canChooseDirectories = NO;
        panel.allowsMultipleSelection = NO;
        panel.allowedFileTypes = @[@"mp3", @"wav", @"ogg", @"aiff", @"aif"];
        void (^complete)(NSModalResponse) = ^(NSModalResponse response) {
            std::lock_guard<std::mutex> lock(choiceMutex);
            const char *path = response == NSModalResponseOK ? panel.URL.path.UTF8String : nullptr;
            if (path) { choicePath = path; choiceState = 1; }
            else choiceState = -1;
            choicePending = false;
        };
        NSWindow *parent = NSApp.mainWindow ?: NSApp.keyWindow;
        if (parent) [panel beginSheetModalForWindow:parent completionHandler:complete];
        else [panel beginWithCompletionHandler:complete];
    });
}

// 0 = pending, -1 = cancelled, -2 = path too long, >0 = UTF-8 byte length.
extern "C" int TigaPollMusicChoice(char *result, int capacity)
{
    std::lock_guard<std::mutex> lock(choiceMutex);
    if (choiceState <= 0) { int state = choiceState; choiceState = 0; return state; }
    choiceState = 0;
    if (choicePath.size() >= (size_t)capacity) return -2;
    memcpy(result, choicePath.c_str(), choicePath.size() + 1);
    return (int)choicePath.size();
}
