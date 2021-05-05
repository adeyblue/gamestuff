using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GameStuff
{
    class Everblue
    {
        static void ParseOutSynthesisTable()
        {
            // everblue
            byte[] fileData = File.ReadAllBytes(@"T:\SLES_506.39");
            MemoryStream ms = new MemoryStream(fileData, false);
            ms.Seek(0x134270, SeekOrigin.Begin);
            BinaryReader br = new BinaryReader(ms);
            using (StreamWriter sw = new StreamWriter(@"T:\everblueSyn.txt"))
            {
                for (; ; )
                {
                    int item1 = br.ReadInt32();
                    if (item1 == 0x1e0014)
                    {
                        break;
                    }
                    int item2 = br.ReadInt32();
                    int resultItem = br.ReadInt32();
                    short percent = br.ReadInt16();
                    short unkBytes = br.ReadInt16();
                    ;
                    sw.WriteLine("0x{0:x4}\t\t0x{1:x4}\t\t0x{2:x4}\t\t{3}", item1, item2, resultItem, percent);
                }
            }
        }
    }
}

#if NOCODE // This is C++
//#include <conio.h>
//#include <string>

HWND FindChildPCSX2Window()
{
	HWND hwnd = FindWindow(L"wxWindowNR", NULL);
	return FindWindowEx(hwnd, NULL, L"wxWindowNR", L"panel");
}

// 1C90630 is array of addresses
// each entry in the array poins to a struct
// first address is 241050
// 
// struct seems to be 0x28 in size (the pointers in the array are 0x28 apart)
struct EverblueItemInfo
{
	int tableIndex; // (seems to increase 1 per item, first is 0, second is 1, etc), 0x0
	char* pAppraisedName; // first description, 0x4
	char* pUnappraisedName; // second description (can be same as first), 0x8
	int buyPrice; // 1000, 0xc
	int sellPrice; // 500, 0x10
	int appraisePrice; // 0x14
	int weight; // 0x18
	short unk3; // 0x1c
	unsigned char unk4; // 0x1e Case 4: (shifted left (24 + 32), right (27 + 32)), case 5: (unk4 & 7)
	unsigned char unk5; // 0x1f Case 7: (shifted left (28 + 32), right (29 + 32))
	char* pSaleDescription; // 0x20
	char* pDesc2; //  0x24
};

//C_ASSERT(sizeof(EverblueItemInfo) == 0x28);

void ReadString(HANDLE hProc, char* pAddress, std::wstring& outStr)
{
	char buffer[33] = {0};
	char* pIter = pAddress;
	SIZE_T read = 0;
	char* pEndIter = NULL;
	std::string newStr;
	do
	{
		if(pIter != pAddress)
		{
			newStr.append(buffer, read);
		}
		memset(buffer, 0, sizeof(buffer));
		read = 0;
		ReadProcessMemory(hProc, pIter, buffer, sizeof(buffer) - 1, &read);
		pIter += read;
	}
	while((pEndIter = (char*)memchr(buffer, 0, read)) == NULL);
	newStr.append(buffer, pEndIter);
	_locale_t pLoc = _get_current_locale();
	// this is because mbstowcs uses the ntdll version which
	// doesn't do the right thing here
	size_t wideChars = _mbstowcs_l(NULL, newStr.c_str(), newStr.length(), pLoc);
	outStr.resize(wideChars);
	_mbstowcs_l(&outStr[0], newStr.c_str(), newStr.length(), pLoc);
}

template<class T>
T IncrementPointerBytes(T val, size_t increment)
{
	ULONG_PTR v = (ULONG_PTR)val;
	v += increment;
	return (T)v;
}

void WriteString(PCWSTR pStr, DWORD len)
{
	DWORD wrote = 0;
	WriteFile(GetStdHandle(STD_OUTPUT_HANDLE), pStr, len, &wrote, NULL);
}

void DumpEverblueItems()
{
	HWND hwnd = FindChildPCSX2Window();
	DWORD procId = 0;
	GetWindowThreadProcessId(hwnd, &procId);
	HANDLE hProc = OpenProcess(PROCESS_VM_READ, FALSE, procId);
	if(!hProc) return;
	//PVOID* pPtrArrayPosEU = (PVOID*)0x21C90630;
	// This is where the item info starts
	//PVOID* pItemInfoPosJP = (PVOID*)0x20239C70;
	PVOID* pPtrArrayPosJP = (PVOID*)0x21c7a270;
	setlocale(LC_ALL, ".932");
	PVOID* pPtrArrayPos = pPtrArrayPosJP;
	SIZE_T bufferSize = 0x1000;
	PVOID* pAddresses = (PVOID*)malloc(bufferSize), *pIter = pAddresses;
	PVOID* pEnd = IncrementPointerBytes(pIter, bufferSize);
	// tsv header
	PCWSTR pHeader = L"\xFEFFItemId\tUnapp.Name\tApp.Cost\tApp.Name\tSalePrice\tBuyPrice\tWeight\tDesc1\tDesc2\n";
	WriteString(pHeader, wcslen(pHeader) * sizeof(*pHeader));
	for(UINT i = 0; i < 23; ++i, pPtrArrayPos = IncrementPointerBytes(pPtrArrayPos, 0x1000))
	{
		ReadProcessMemory(hProc, (PVOID)pPtrArrayPos, pAddresses, bufferSize, NULL);
		for(pIter = pAddresses; pIter < pEnd; ++pIter)
		{
			if(!*pIter)
			{
				//printf("Found null pointer at %p\n\n", pPtrArrayPos + (pIter - pAddresses));
				goto endLoop;
			}
			PVOID pInfoAddr = IncrementPointerBytes(*pIter, 0x20000000);
			EverblueItemInfo itemInfo = {0};
			ReadProcessMemory(hProc, pInfoAddr, &itemInfo, sizeof(itemInfo), NULL);
			if(itemInfo.tableIndex == 0x9fff)
			{
				continue;
			}
			std::wstring name1, name2, desc, desc2;
			ReadString(hProc, IncrementPointerBytes(itemInfo.pAppraisedName, 0x20000000), name1);
			if(itemInfo.pAppraisedName != itemInfo.pUnappraisedName)
			{
				ReadString(hProc, IncrementPointerBytes(itemInfo.pUnappraisedName, 0x20000000), name2);
			}
			else name2 = name1;
			ReadString(hProc, IncrementPointerBytes(itemInfo.pSaleDescription, 0x20000000), desc);
			ReadString(hProc, IncrementPointerBytes(itemInfo.pDesc2, 0x20000000), desc2);
			ULONGLONG shiftHelper = itemInfo.unk4;
			shiftHelper <<= (24 + 32);
			LONGLONG signedShiftHelper1 = (LONGLONG)shiftHelper;
			signedShiftHelper1 >>= (27 + 32);
			shiftHelper = itemInfo.unk5;
			shiftHelper <<= (28 + 32);
			LONGLONG signedShiftHelper2 = (LONGLONG)shiftHelper;
			signedShiftHelper2 >>= (29 + 32);
			WCHAR bigBuffer[500] = {0};
			int wideChars = _snwprintf(
				bigBuffer,
				ARRAYSIZE(bigBuffer),
				L"0x%x\t%s\t%d\t%s\t%d\t%d\t%d\t%s\t%s\n",
				itemInfo.tableIndex,
				name2.c_str(),
				itemInfo.appraisePrice,
				name1.c_str(),
				itemInfo.sellPrice,
				itemInfo.buyPrice,
				itemInfo.weight,
				desc.c_str(),
				desc2.c_str()
			);
			WriteString(bigBuffer, wideChars * sizeof(*bigBuffer));
			/*printf(
				"Item id: %#04x\nAppraised Name: %ws\nUnappraised Name: %ws\nDescription: %ws\nDescription 2: %ws\nBuy price: %dG, sell price: %dG\n"
				"Appraise Price: %d, Weight: %dg, unk3: %#hx\nUnk4 (raw): %#x, unk4 (case 4) %#x, (case 5): %#x\nUnk 5 (raw): %#x, unk 5 (case 7): %#x\n\n",
				itemInfo.tableIndex,
				name1.c_str(), 
				name2.c_str(),
				desc.c_str(),
				desc2.c_str(),
				itemInfo.buyPrice, itemInfo.sellPrice,
				itemInfo.appraisePrice,
				itemInfo.weight,
				itemInfo.unk3,
				itemInfo.unk4,
				(int)(signedShiftHelper1 & 0xFFFFFFFF),
				itemInfo.unk4 & 7,
				itemInfo.unk5,
				(int)(signedShiftHelper2 & 0XFFFFFFFF)
			);*/
		}
	}
endLoop:
	CloseHandle(hProc);
	free(pAddresses);
}

#endif