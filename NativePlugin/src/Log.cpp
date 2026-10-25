#include "Log.h"

#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <deque>
#include <mutex>
#include <string>

namespace
{
    struct Entry
    {
        UnityLogType type;
        std::string  message;
    };

    // Messages logged before C# starts draining (e.g. during UnityPluginLoad) are kept until
    // read; beyond this the oldest are dropped.
    constexpr size_t kMaxQueued = 256;

    std::mutex        s_mutex;
    IUnityLog*        s_unityLog = nullptr;
    std::deque<Entry> s_queue;

    void Write(UnityLogType type, const char* format, va_list args)
    {
        char body[1024];
        vsnprintf(body, sizeof(body), format, args);

        char message[1100];
        snprintf(message, sizeof(message), "[QuadBuffer native] %s", body);

        IUnityLog* unityLog;
        {
            std::lock_guard<std::mutex> lock(s_mutex);
            unityLog = s_unityLog;
            if (s_queue.size() >= kMaxQueued)
                s_queue.pop_front();
            s_queue.push_back({ type, message });
        }

        // Called outside the lock: Unity may forward the message to managed log handlers.
        if (unityLog)
            unityLog->Log(type, message, __FILE__, __LINE__);
    }
}

namespace QuadBufferLog
{
    void SetUnityLog(IUnityLog* log)
    {
        std::lock_guard<std::mutex> lock(s_mutex);
        s_unityLog = log;
    }

    void Info(const char* format, ...)
    {
        va_list args;
        va_start(args, format);
        Write(kUnityLogTypeLog, format, args);
        va_end(args);
    }

    void Warning(const char* format, ...)
    {
        va_list args;
        va_start(args, format);
        Write(kUnityLogTypeWarning, format, args);
        va_end(args);
    }

    void Error(const char* format, ...)
    {
        va_list args;
        va_start(args, format);
        Write(kUnityLogTypeError, format, args);
        va_end(args);
    }

    bool Pop(int* type, char* buffer, int bufferSize)
    {
        std::lock_guard<std::mutex> lock(s_mutex);
        if (s_queue.empty() || !buffer || bufferSize <= 0)
            return false;

        const Entry& entry = s_queue.front();
        if (type)
            *type = entry.type;
        strncpy_s(buffer, static_cast<size_t>(bufferSize), entry.message.c_str(), _TRUNCATE);
        s_queue.pop_front();
        return true;
    }
}
