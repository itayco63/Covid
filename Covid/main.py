import getopt
import sys
import os

import numpy as np
import pandas as pd
import tensorflow as tf
from sklearn.utils import shuffle
from tensorflow.keras import Input
from tensorflow.keras import layers
from tensorflow.keras.callbacks import ModelCheckpoint, ReduceLROnPlateau, EarlyStopping
from tensorflow.keras.models import Model
from tqdm import tqdm
import geopandas as gpd

sequence_length = 20
train_df_file_path = 'Covid\\covid19-global-forecasting-week-4.csv'
extra_data_file_path = 'Covid\\enriched_covid_19_week_2.csv'
best_model_file_path = 'Covid\\best_model.h5'
trend_df_file_path = 'Covid\\trend_df.pkl'
validation_file_path = 'Covid\\validation.json'


def prep_data():
    # Get cases data up till mid May
    train_df = gpd.read_file(str(os.getcwd()+'\\'+train_df_file_path))
    train_df["ConfirmedCases"] = train_df["ConfirmedCases"].astype("float")
    train_df["Country_Region"] = [row.Country_Region.replace("'", "").strip(" ") if row.Province_State == "" else str(
        row.Country_Region + "_" + row.Province_State).replace("'", "").strip(" ") for idx, row in train_df.iterrows()]

    # Get countries data
    extra_data_df = gpd.read_file(str(os.getcwd()+'\\'+extra_data_file_path))
    extra_data_df["Country_Region"] = [country_name.replace("'", "") for country_name in
                                       extra_data_df["Country_Region"]]
    extra_data_df["restrictions"] = extra_data_df["restrictions"].astype("int")
    extra_data_df["quarantine"] = extra_data_df["quarantine"].astype("int")
    extra_data_df["schools"] = extra_data_df["schools"].astype("int")
    extra_data_df["density"] = extra_data_df["density"].astype("float")
    extra_data_df["hospibed"] = extra_data_df["hospibed"].astype("float")
    extra_data_df["lung"] = extra_data_df["lung"].astype("float")
    extra_data_df["total_pop"] = extra_data_df["total_pop"].astype("float")
    extra_data_df["total_pop"] = extra_data_df["total_pop"] / max(extra_data_df["total_pop"])
    extra_data_df["density"] = extra_data_df["density"] / max(extra_data_df["density"])
    extra_data_df["hospibed"] = extra_data_df["hospibed"] / max(extra_data_df["hospibed"])
    extra_data_df["lung"] = extra_data_df["lung"] / max(extra_data_df["lung"])
    extra_data_df["age_100+"] = extra_data_df["age_100+"].astype("float")
    extra_data_df["age_100+"] = extra_data_df["age_100+"] / max(extra_data_df["age_100+"])

    extra_data_df = extra_data_df[
        ["Country_Region", "Date", "restrictions", "quarantine", "schools", "hospibed", "lung", "total_pop", "density",
         "age_100+"]]
    extra_data_df.head()

    train_df = train_df.merge(extra_data_df, how="left", on=['Country_Region', 'Date']).drop_duplicates()
    train_df.head()

    # Complete the data till mid May
    for country_region in train_df.Country_Region.unique():
        query_df = train_df.query("Country_Region=='" + country_region + "' and Date=='2020-03-25'")
        train_df.loc[(train_df["Country_Region"] == country_region) & (train_df["Date"] > "2020-03-25"), "total_pop"] = \
            query_df.total_pop.values[0]
        train_df.loc[(train_df["Country_Region"] == country_region) & (train_df["Date"] > "2020-03-25"), "hospibed"] = \
            query_df.hospibed.values[0]
        train_df.loc[(train_df["Country_Region"] == country_region) & (train_df["Date"] > "2020-03-25"), "density"] = \
            query_df.density.values[0]
        train_df.loc[(train_df["Country_Region"] == country_region) & (train_df["Date"] > "2020-03-25"), "lung"] = \
            query_df.lung.values[0]
        train_df.loc[(train_df["Country_Region"] == country_region) & (train_df["Date"] > "2020-03-25"), "age_100+"] = \
            query_df["age_100+"].values[0]
        train_df.loc[
            (train_df["Country_Region"] == country_region) & (train_df["Date"] > "2020-03-25"), "restrictions"] = \
            query_df.restrictions.values[0]
        train_df.loc[(train_df["Country_Region"] == country_region) & (train_df["Date"] > "2020-03-25"), "quarantine"] = \
            query_df.quarantine.values[0]
        train_df.loc[(train_df["Country_Region"] == country_region) & (train_df["Date"] > "2020-03-25"), "schools"] = \
            query_df.schools.values[0]

    median_pop = np.median(extra_data_df.total_pop)
    median_hospibed = np.median(extra_data_df.hospibed)
    median_density = np.median(extra_data_df.density)
    median_lung = np.median(extra_data_df.lung)
    median_centenarian_pop = np.median(extra_data_df["age_100+"])
    for country_region in train_df.Country_Region.unique():
        if extra_data_df.query("Country_Region=='" + country_region + "'").empty:
            train_df.loc[train_df["Country_Region"] == country_region, "total_pop"] = median_pop
            train_df.loc[train_df["Country_Region"] == country_region, "hospibed"] = median_hospibed
            train_df.loc[train_df["Country_Region"] == country_region, "density"] = median_density
            train_df.loc[train_df["Country_Region"] == country_region, "lung"] = median_lung
            train_df.loc[train_df["Country_Region"] == country_region, "age_100+"] = median_centenarian_pop
            train_df.loc[train_df["Country_Region"] == country_region, "restrictions"] = 0
            train_df.loc[train_df["Country_Region"] == country_region, "quarantine"] = 0
            train_df.loc[train_df["Country_Region"] == country_region, "schools"] = 0

    # Create one data frame of trends with all data, with multiples of 3 weeks
    trend_df = pd.DataFrame(
        columns={"infection_trend", "quarantine_trend", "school_trend", "total_population",
                 "expected_cases"})
    train_df = train_df.query("Date>'2020-01-22'and Date<'2020-05-13'")
    days_in_sequence = 21

    trend_list = []

    with tqdm(total=len(list(train_df.Country_Region.unique()))) as pbar:
        for country in train_df.Country_Region.unique():
            for province in train_df.query(f"Country_Region=='{country}'").Province_State.unique():
                province_df = train_df.query(f"Country_Region=='{country}' and Province_State=='{province}'")
                for i in range(0, len(province_df), int(days_in_sequence / 3)):
                    if i + days_in_sequence <= len(province_df):
                        # prepare all the temporal inputs
                        infection_trend = [float(x) for x in
                                           province_df[i:i + days_in_sequence - 1].ConfirmedCases.values]
                        expected_cases = float(province_df.iloc[i + days_in_sequence - 1].ConfirmedCases)

                        # Remove "bad" trends
                        if infection_trend[1] < infection_trend[0]:
                            infection_trend[0] = infection_trend[1]
                        for index in range(1, days_in_sequence - 3):
                            if infection_trend[index + 1] < infection_trend[index]:
                                infection_trend[index] = (infection_trend[index + 1] + infection_trend[index - 1]) / 2
                        if expected_cases < infection_trend[days_in_sequence - 2]:
                            infection_trend[days_in_sequence - 2] = (infection_trend[
                                                                         days_in_sequence - 3] + expected_cases) / 2

                        is_bad_trend = False
                        for index2 in range(0, days_in_sequence - 2):
                            if infection_trend[index2 + 1] < infection_trend[index2]:
                                is_bad_trend = True

                        if is_bad_trend:
                            continue

                        restriction_trend = [float(x) for x in
                                             province_df[i:i + days_in_sequence - 1].restrictions.values]
                        quarantine_trend = [float(x) for x in province_df[i:i + days_in_sequence - 1].quarantine.values]
                        school_trend = [float(x) for x in province_df[i:i + days_in_sequence - 1].schools.values]
                        dates = [x for x in province_df[i:i + days_in_sequence].Date.values]

                        # preparing all the demographic inputs
                        total_population = float(province_df.iloc[i].total_pop)
                        density = float(province_df.iloc[i].density)
                        hospibed = float(province_df.iloc[i].hospibed)
                        lung = float(province_df.iloc[i].lung)
                        centenarian_pop = float(province_df.iloc[i]["age_100+"])

                        trend_list.append({"country_name": country,
                                           "infection_trend": infection_trend,
                                           "restriction_trend": restriction_trend,
                                           "quarantine_trend": quarantine_trend,
                                           "school_trend": school_trend,
                                           "demographic_inputs": [total_population, density, hospibed, lung,
                                                                  centenarian_pop],
                                           "expected_cases": expected_cases,
                                           "dates": dates})
            pbar.update(1)
    trend_df = pd.DataFrame(trend_list)

    trend_df["temporal_inputs"] = [np.asarray(
        [trends["infection_trend"], trends["restriction_trend"], trends["quarantine_trend"],
         trends["school_trend"]]) for idx, trends in trend_df.iterrows()]

    # Remove 0 cases trends
    temp_df = pd.DataFrame()
    for idx, row in trend_df.iterrows():
        if sum(row.infection_trend) > 0:
            temp_df = temp_df.append(row)
    trend_df = temp_df
    trend_df.index = np.arange(0, len(trend_df))

    trend_df.to_pickle(os.getcwd()+'\\'+trend_df_file_path)

    return trend_df


def split_training_validation(trend_df):
    print("Trends list size: ", len(trend_df))

    # Save every last trend of each country in validation
    training_df = pd.DataFrame()
    tmp_df = pd.DataFrame()
    validation_df = pd.DataFrame()

    country_name_list = trend_df["country_name"].values
    country_name_array = np.array(country_name_list)
    for idx, row in trend_df.iterrows():
        if idx == len(country_name_array) - 1:
            validation_df = validation_df.append(row)
            break
        if country_name_array[idx] != country_name_array[idx + 1]:
            validation_df = validation_df.append(row)
        else:
            tmp_df = tmp_df.append(row)

    tmp_df = shuffle(tmp_df)
    validation_df = shuffle(validation_df)

    # Add more random 10% to validation to get ~20% validation, 80% training
    training_percentage = 0.9
    for idx, row in tmp_df.iterrows():
        if idx < tmp_df.shape[0] * training_percentage:
            training_df = training_df.append(row)
        else:
            validation_df = validation_df.append(row)

    training_item_count = training_df.shape[0]
    validation_item_count = validation_df.shape[0]

    print("Training size: ", training_item_count)
    print("Validation size: ", validation_item_count)

    # Create data sets for training and validation
    X_temporal_train = np.asarray(np.transpose(
        np.reshape(np.asarray([np.asarray(x) for x in training_df["temporal_inputs"].values]),
                   (training_item_count, 4, sequence_length)), (0, 2, 1))).astype(np.float32)
    X_demographic_train = np.asarray([np.asarray(x) for x in training_df["demographic_inputs"]]).astype(np.float32)
    Y_cases_train = np.asarray([np.asarray(x) for x in training_df["expected_cases"]]).astype(np.float32)
    train_countries_name = np.asarray([np.asarray(x) for x in training_df["country_name"]])
    train_dates = np.asarray([np.asarray(x) for x in training_df["dates"]])

    X_temporal_test = np.asarray(np.transpose(
        np.reshape(np.asarray([np.asarray(x) for x in validation_df["temporal_inputs"]]),
                   (validation_item_count, 4, sequence_length)), (0, 2, 1))).astype(np.float32)
    X_demographic_test = np.asarray([np.asarray(x) for x in validation_df["demographic_inputs"]]).astype(np.float32)
    Y_cases_test = np.asarray([np.asarray(x) for x in validation_df["expected_cases"]]).astype(np.float32)
    test_countries_name = np.asarray([np.asarray(x) for x in validation_df["country_name"]])
    test_dates = np.asarray([np.asarray(x) for x in validation_df["dates"]])

    return X_temporal_train, X_demographic_train, Y_cases_train, X_temporal_test, X_demographic_test, Y_cases_test,\
           test_countries_name, train_dates, test_dates, validation_df


def build_model():
    # temporal input layer
    temporal_input_layer = Input(shape=(sequence_length, 4))
    main_rnn_layer = layers.LSTM(64, return_sequences=True, recurrent_dropout=0.2)(temporal_input_layer)

    # demographic input layer
    demographic_input_layer = Input(shape=(5))
    demographic_dense = layers.Dense(16)(demographic_input_layer)
    demographic_dropout = layers.Dropout(0.2)(demographic_dense)

    # cases output layer
    rnn_c = layers.LSTM(32)(main_rnn_layer)
    merge_c = layers.Concatenate(axis=-1)([rnn_c, demographic_dropout])
    dense_c = layers.Dense(128)(merge_c)
    dropout_c = layers.Dropout(0.3)(dense_c)
    cases = layers.Dense(1, activation=layers.LeakyReLU(alpha=0.1), name="cases")(dropout_c)

    model = Model([temporal_input_layer, demographic_input_layer], [cases])

    model.summary()

    return model


def train_model(model, X_temporal_train, X_demographic_train, Y_cases_train, X_temporal_test,
                X_demographic_test, Y_cases_test):
    callbacks = [ReduceLROnPlateau(monitor='val_loss', patience=4, verbose=1, factor=0.6),
                 EarlyStopping(monitor='val_loss', patience=20),
                 ModelCheckpoint(filepath=os.getcwd()+'\\'+best_model_file_path, monitor='val_loss', save_best_only=True)]
    model.compile(loss=[tf.keras.losses.MeanSquaredLogarithmicError(), tf.keras.losses.MeanSquaredLogarithmicError()],
                  optimizer="adam")
    history = model.fit([X_temporal_train, X_demographic_train], [Y_cases_train],
                        epochs=300,
                        batch_size=16,
                        validation_data=([X_temporal_test, X_demographic_test], [Y_cases_test]),
                        callbacks=callbacks)


def gen_prediction(model, X_temporal_test, X_demographic_test, best_model_file_path, validation_df):
    model.load_weights(str(os.getcwd()+'\\'+best_model_file_path))
    predictions = model.predict([X_temporal_test, X_demographic_test])

    validation_df['Prediction'] = predictions
    validation_df.to_json(os.getcwd()+'\\'+validation_file_path, orient='records')


#################################### Main: ###############################
full_cmd_arguments = sys.argv
argument_list = full_cmd_arguments[1:]
short_options = "mt"
long_options = ["help", "map_trends", "train_lstm"]
try:
    arguments, values = getopt.getopt(argument_list, short_options, long_options)
except getopt.error as err:
    print(str(err))
    sys.exit(2)

map_trends = False
train_lstm = False
for current_argument, current_value in arguments:
    if current_argument in ("-m", "--map_trends"):
        map_trends = True
    elif current_argument in ("-t", "--train_lstm"):
        train_lstm = True

if map_trends:
    prep_data()
trend_df = pd.read_pickle(os.getcwd()+'\\'+trend_df_file_path)
X_temporal_train, X_demographic_train, Y_cases_train, X_temporal_test, X_demographic_test, Y_cases_test,\
test_countries_name, train_dates, test_dates, validation_df = split_training_validation(
    trend_df)
model = build_model()
if train_lstm:
    train_model(model, X_temporal_train, X_demographic_train, Y_cases_train, X_temporal_test, X_demographic_test,
                Y_cases_test)
gen_prediction(model, X_temporal_test, X_demographic_test, best_model_file_path, validation_df)
