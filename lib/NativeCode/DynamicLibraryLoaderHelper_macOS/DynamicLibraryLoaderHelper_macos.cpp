/*
 * Copyright (c) 2021 PlayEveryWare
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in 
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

// DynamicLibraryLoaderHelper.cpp : Defines the functions for the static library.
//

#include <assert.h>
#include <dlfcn.h>
#include <stdio.h>
#include <map>
#include <string>
#include <libgen.h>
#include <mach-o/dyld.h>

#define STATIC_EXPORT(return_type) extern "C" return_type

std::map <std::string,std::string> baseNameToPath;

struct DLLHContext
{
};

//-------------------------------------------------------------------------
STATIC_EXPORT(void*) LoadLibrary(const char *library_path)
{
    std::string filename = std::string(basename((char*)library_path));
    std::string stemname = filename.substr(0, filename.find_last_of("."));
   
    void* handle = dlopen(library_path, RTLD_NOW);
    if(handle == nullptr)
    {
        return nullptr;
    }
    
    baseNameToPath[stemname] = std::string(library_path);

    return handle;
}

//-------------------------------------------------------------------------
// pretend windows like function
STATIC_EXPORT(bool) FreeLibrary(void *library_handle)
{
    dlclose(library_handle);
    
    if(dlerror())
    {
        return false;
    }
    
    return true;
}

//-------------------------------------------------------------------------
STATIC_EXPORT(void*) GetModuleHandle(const char *stemname)
{
    if(baseNameToPath.find(stemname) == baseNameToPath.end())
    {
        return nullptr;
    }
        
    void *to_return = dlopen(baseNameToPath[stemname].c_str(), RTLD_NOLOAD);
    
    if(to_return == nullptr)
    {
        baseNameToPath.erase(stemname);
    }
    // dlopen increments the ref handle, so make sure to 
    // release the ref handle. See `man dlopen`
    if (to_return)
    {
        dlclose(to_return);
    }

    return to_return;
}

//-------------------------------------------------------------------------
STATIC_EXPORT(void*) GetProcAddress(void *library_handle, const char *function_name)
{
    return dlsym(library_handle, function_name);
}

//-------------------------------------------------------------------------
STATIC_EXPORT(void) GetError()
{
    char *errstr;

    errstr = dlerror();
    
    freopen("debug.txt", "w", stdout);
    printf ("TryDynamicLinking \n");
    if (errstr != NULL)
    printf ("A dynamic linking error occurred: (%s)\n", errstr);
    fclose (stdout);

}

STATIC_EXPORT(void) PrintLibs()
{
    freopen("libs.txt", "w", stdout);
    for(uint32_t i=0;i<_dyld_image_count();i++)
    {
        printf("lib num %d : %s\n",i,_dyld_get_image_name(i));
    }
    fclose (stdout);
}

//-------------------------------------------------------------------------
void * DLLH_macOS_load_library_at_path(DLLHContext *ctx, const char *library_path)
{
    void *to_return = dlopen(library_path, RTLD_NOW);
   
    return to_return; 
}

//-------------------------------------------------------------------------
// TODO: Handle the actual module instead of all symbols
void * DLLH_macOS_load_function_with_name(DLLHContext *ctx, void *library_handle, const char *function)
{
    void *output_ptr = nullptr;

    output_ptr = dlsym(library_handle, function);

    return output_ptr;
}

//-------------------------------------------------------------------------
// Create heap data for storing random things, if need be on a given platform
STATIC_EXPORT(void *) DLLH_create_context()
{
    return new DLLHContext();
}

//-------------------------------------------------------------------------
STATIC_EXPORT(void) DLLH_destroy_context(void *context)
{
    delete static_cast<DLLHContext *>(context);
}

//-------------------------------------------------------------------------
STATIC_EXPORT(void *) DLLH_load_library_at_path(void *ctx, const char *library_path)
{
    if (ctx == nullptr) {
        return nullptr;
    }

    DLLHContext *dllh_ctx = static_cast<DLLHContext*>(ctx);
    void *to_return = nullptr;
    
    to_return = DLLH_macOS_load_library_at_path(dllh_ctx, library_path);

    return to_return;
}

//-------------------------------------------------------------------------
// This returns a bare function pointer that is only valid as long as the library_handle and context are
// valid
STATIC_EXPORT(void *) DLLH_load_function_with_name(void *ctx, void *library_handle, const char *function)
{
    void *to_return = nullptr;
    DLLHContext *dllh_ctx = static_cast<DLLHContext*>(ctx);

    to_return = DLLH_macOS_load_function_with_name(dllh_ctx, library_handle, function);

    return to_return;
}

//-------------------------------------------------------------------------
// TODO: unload the library correct? I don't know if that's actually a good
// idea on macos or not
STATIC_EXPORT(void) DLLH_unload_library_at_path(const char *library_path)
{
}

