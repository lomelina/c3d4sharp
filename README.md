# c3d4sharp
C3D File reading/writing tools written in C#

c3d4Sharp is a lightweight C# library provided with sources without any dependencies on external libraries. This library contains a simple reader and writer for loading and producing C3D files without having all data in memory (it's processing files as a stream of positional data (and analog data as well)). It's useful when reading and writing really large C3D files.

It's implemented according to [http://c3d.org/pdf/c3dformat_ug.pdf Motion Lab's] specification. This library is NOT a full spec implementation (so far), but it covers a large part of the spec.

Main characteristic: - C3dReader does not read the whole file at once. It reads frame by frame after calling the ReadFrame() method. This is useful especially in case of large c3d files.
