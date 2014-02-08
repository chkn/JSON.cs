JSON.cs
=======

Fast, light JSON parse and stringify.

Clone
-----

If your project uses git, just add this repo as a submodule like so:

    git submodule add https://github.com/chkn/JSON.cs.git JSON



Usage
-----

Add JSON/JSON.cs to your project. You may also want to define a couple preprocessor defines (copied from the comment in JSON.cs):

    //    ENABLE_DYNAMIC - enable a dynamic overload of JSON.Parse
    //    NET_45         - define this for Windows Store or PCL Profiles
    //                        without classic reflection...


There are 2 APIs that should be pretty self-explanatory:

    JSON.Stringify (object someObject)
    JSON.Parse (string someJson);
