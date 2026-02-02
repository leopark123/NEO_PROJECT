// clogik_ser.cpp : This file contains the 'main' function. Program execution begins and ends there
//Example of parsing Cerbralogik module serial V5.0
//Compiled with VS 2017

#include "pch.h"
#include <iostream>
#include "Serial_c.h"
#include "sysinfoapi.h"

unsigned short Phase = 0;
unsigned short Crc = 0;
unsigned short RdyFlg = 0;
unsigned short LocalIndex = 0;
unsigned short MsgCRC = 0;
short data1[18];
unsigned short packet = 0;
FILE *out_gs, *out_eeg;
Serial *serial;

// Parser of read packets
void parser(int size, unsigned char *buffer) {
	int i;
	int indx;
	for (i = 0; i < size; i++)
	{
		switch (Phase) {
		case(0): { //header 1st byte
			//Check if it == AA
			if (buffer[i] == 0xAA) {
				Phase = 1;
				Crc = 0xAA;
			}//if
			break;
		}//case

		case(1): { //header 2nd byte
			//Check if it == 55
			if (buffer[i] == 0x55) {
				Phase = 2; // data collection pahse
				RdyFlg = 0;
				Crc += 0x55;
				LocalIndex = 0;
			}//if
			break;
		}//case

		case (2): {// data collection
			indx = LocalIndex / 2;
			if ((LocalIndex % 2) == 0)
				data1[indx] = ( short)((buffer[i] & 0xff) << 8); //low byte
			else
				data1[indx] |= buffer[i]; //high byte
			LocalIndex++;
			Crc += buffer[i];
			if (LocalIndex == 36) {
				Phase = 3; //CRC low byte phase
			}

			break;
		}//case

		case(3): { //CRC low byte
			MsgCRC = (unsigned short)((buffer[i] & 0xff) << 8);
			Phase = 4; //CRC high byte phase
			break;
		}//case

		case(4): { //CRC high byte
			MsgCRC += buffer[i];
			if (MsgCRC == Crc) { // packet checksum is valid ?
				RdyFlg = 1;
				packet++;

				// GS Histogram output
			    if (data1[16] != 255) {
					fprintf_s(out_gs, "%d, %d, %d ", packet, data1[3], data1[16]); //ch1 GS info- packet# + AEEG GS Histogram bins  + counter 0-229 
					fprintf_s(out_gs, "\n");
				};

				//if (data[16]==229) // time to draw aEEG GS every 15 seconds
				//	fprintf_s(out_gs, "******* end of one GS channel 1 ****** \n");

				//EEG output
				fprintf_s(out_eeg, "%d, %d, %f, %08X\n", packet, data1[0], ((float)data1[0])*0.076, data1[9]); // EEG ch1 info - packet# + eeg raw data + eeg uV + config
			}
			else
				fprintf_s(stdout, "Error packet %d\n", packet);  

			Phase = 0; // next packet phase
			break;
		}//case

		} // swich
	}
}

// Config filters
void setParam(unsigned char CommandType , unsigned char CommandOpt) {
	unsigned char cmd[8];
	unsigned char CRC = 0;
	cmd[0] = 0xAA;
	cmd[1] = 0x55;
	cmd[2] = 0x0;
	cmd[3] = (CommandType << 4 ) | CommandOpt;
	cmd[4] = 0x0;
	cmd[5] = 0x0;
	CRC = cmd[0] + cmd[1] + cmd[2] + cmd[3] + cmd[4] + cmd[5];
	cmd[6] = CRC;
	cmd[7] = 0x3;
	serial->write(cmd, sizeof(cmd));
}
		int main()
		{
			 char buffer[1000];
			 int t1, delta;
			int size, err;                                                              
			string comm;
			std::cout << "Init Cerebralogik 5.0 \n";
			comm = "\\\\.\\COM1";
			serial = new Serial(comm, 115200);  
			if (serial->status() != Serial::ERR_OK) {
				printf("Serial error %d \n", serial->status());
				return -1;
			}
			if ((err = fopen_s(&out_eeg, "c:\\clogik_50_eeg.csv", "w")) != 0) { // output file EEG wf ch1
				fprintf_s(stdout, "Error output_eeg\n");
			};

			if ((err = fopen_s(&out_gs, "c:\\clogik_50_gs.csv", "w")) != 0) { // output file aEEG GS  wf ch1
				fprintf_s(stdout, "Error output_eeg\n");
			};

			Phase = 0;
			Sleep(200);
			serial->setFuncState(CLRRTS); //Reset module
			Sleep(1500);  
			serial->setFuncState(SETRTS);
			Sleep(5000);
			setParam(0x1, 1); // notch filter 50 Hz
			setParam(0x2, 1); // high pass  filter  0.3 Hz
			setParam(0x3, 1); // low pass filter 15 Hz
			Sleep(200);
			t1= GetTickCount();  
			std::cout << "Start collect " << t1 << "\n";
			while ( true) {
				delta = GetTickCount() - t1;
				size = serial->read(buffer, 1000, false);
				if (size != 0) {
					if (delta > 10000) //start collect after 10 sec
						parser(size, (unsigned char*) buffer);
					if (packet > 160 * 60 * 2) // 2 min data
						break;
				}
			}
			std::cout << "End Cerebralogik 5.0 " << packet << " End";
	
	delete serial;
	fclose(out_eeg);
	fclose(out_gs);
}
