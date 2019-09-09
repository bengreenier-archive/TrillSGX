// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#include <thread>
#include <string>
#include <ctime>

// global function ptr to the callback function
// set by RegisterNativeCallback
static void (*s_managed_cb)() = NULL;

// thread-running state flag
// set to false to make thread terminate/join safely
static volatile bool s_proc_run = true;

// processing thread - simulates pumping data from the native side
// and raising the managed callback when data is had
static std::thread s_proc;

// temporary storage for data - read IO-style by the managed side
static std::wstring s_buf;

// DLL entrypoint (os calls this for us)
BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
                     )
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
		// we'll need rand
		std::srand(std::time(nullptr));

		// create (and save) our thread for pumping
		s_proc = std::thread([]() {
			while (s_proc_run) {
				// if we have a managed callback, and aren't "full" of data
				// we go ahead and generate some, calling the callback
				if (s_managed_cb != NULL && s_buf.empty()) {
					// we generate two different types of data
					// this makes our "filter" more interesting
					int random_flip = std::rand();
					if (random_flip > RAND_MAX / 2) {
						s_buf.append(L"hello world");
					}
					else {
						s_buf.append(L"goodbye world");
					}
					// call the managed callback
					// note: this will trigger a all to ReceiveCallbackData
					s_managed_cb();
				}
				// wait a bit before pumping again
				std::this_thread::sleep_for(std::chrono::seconds(5));
			}
		});
		break;
    case DLL_THREAD_ATTACH:
		break;
    case DLL_THREAD_DETACH:
		break;
    case DLL_PROCESS_DETACH:
		s_proc_run = false;
		s_proc.join();
        break;
    }
    return TRUE;
}

extern "C" {
	// WOOOOOOOOOOOOOOO LETS GET IT CPP IS LIT
	#define API __declspec(dllexport) __cdecl

	// Allows the managed side to hand us a callback function ptr
	void API RegisterNativeCallback(void (*cb)()) {
		s_managed_cb = cb;
	}

	// Allows the managed side to read our data buffer, io style
	// That is to say: This function is called to read a chunk of data
	// The amount read is returned
	// When all data is read, 0 is returned
	int API ReceiveCallbackData(wchar_t* buffer, int max) {
		if (!s_buf.empty()) {
			int copied_amount = s_buf.copy(buffer, max);
			buffer[copied_amount] = '\0';
			s_buf.erase(0, max);

			return copied_amount + 1;
		} else {
			return 0;
		}
	}

	// Basic matching "filter" if our data contains hello, match
	bool API MatchData(const wchar_t* buffer, int max) {
		std::wstring data(buffer, max);

		if (data.find(L"hello") != std::wstring::npos) {
			return true;
		} else {
			return false;
		}
	}

	// Allows the managed side to cleanup the callback function pointer
	void API UnregisterNativeCallback() {
		s_managed_cb = NULL;
	}
}