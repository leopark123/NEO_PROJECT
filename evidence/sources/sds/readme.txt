The clogik_50_ser.cpp and Serial_c.cpp is example of reading and parsing  protocol of cerebralogik V5.0 module:
The output files of this example are: 
1. csv file of eeg wf (clogik_50_eeg.csv) of ch1.
2. cvs file of aEEg GS file (clogik_50_gs.csv) of ch1.

EEG Wf:
The clogik_50_eeg_graph.xlsx file created manually from clogik_50_eeg.csv by MS excel to show the EEG WF.
Notice that the sample rate of EEG Wf is 160 samples per second.

aEEG GS:
The clogik_50_gs.csv file contains the histogram value for aEEG GS of range 0-200 uV , related  to 0-229 elements.
When The counter is 255 the GS data should ignored, when the counter is 229 this is the end of 15 seconds of historam data.




 