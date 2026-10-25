#pragma once

#include "IUnityInterface.h"
#include "IUnityLog.h"

/**
 * @brief Plugin logging.
 *
 * Each message goes to Unity's log through IUnityLog (Editor Console / Player.log) and into a
 * bounded queue that the C# side drains with QuadBuffer_PopLogMessage() to show in the
 * in-game console. Safe to call from any thread.
 */
namespace QuadBufferLog
{
    /**
     * @brief Sets the Unity log interface.
     * @param log Unity's log interface, or `nullptr` on unload (messages are then only queued).
     */
    void SetUnityLog(IUnityLog* log);

    /**
     * @brief Logs an informational message.
     * @param format printf-style format string, followed by its arguments.
     */
    void Info(const char* format, ...);

    /**
     * @brief Logs a warning.
     * @param format printf-style format string, followed by its arguments.
     */
    void Warning(const char* format, ...);

    /**
     * @brief Logs an error.
     * @param format printf-style format string, followed by its arguments.
     */
    void Error(const char* format, ...);

    /**
     * @brief Removes the oldest queued message.
     * @param[out] type       UnityLogType of the message.
     * @param[out] buffer     Receives the null-terminated message, truncated to fit.
     * @param      bufferSize Size of @p buffer in bytes.
     * @return `false` if the queue was empty.
     */
    bool Pop(int* type, char* buffer, int bufferSize);
}
