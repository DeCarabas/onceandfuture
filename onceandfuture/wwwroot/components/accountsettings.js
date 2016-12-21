import React from 'react';
import { connect } from 'react-redux';
import {
  COLOR_VERY_LIGHT,

  SIZE_BANNER_HEIGHT,

  Z_INDEX_ACCOUNT_SETTINGS,
} from './style';
import {
  accountSettingsToggle,
} from '../actions';
import {
  SettingInputBox,
  SettingPasswordBox,
  SettingsSectionTitle,
} from './settingscontrols';
import IconButton from './iconbutton';

const HeaderBox = ({onClose}) => {
  const style = {
    position: "relative",
    height: SIZE_BANNER_HEIGHT,
  };

  const h_style = {
    position: "absolute",
    top: 5,
    margin: 0,
  };

  const b_style = {
    position: "absolute",
    top: 0, right: 0,
  };

  return <div style={style}>
    <h2 style={h_style}>Account Settings</h2>
    <div style={b_style}>
      <IconButton tip="Close" icon="fa-window-close" onClick={onClose} />
    </div>
  </div>;
}

const ChangeEmailBox = ({address, setAddress}) => {
  return (
    <div>
      <SettingsSectionTitle text="Change Email Address" />
      <p>Change your email address.</p>
      <SettingInputBox value={address} setValue={setAddress} buttonLabel='Change' />
    </div>
  );
};

const ChangePasswordBox = ({setPassword}) => {
  return (
    <div>
      <SettingsSectionTitle text="Change Password" />
      <p>Change your email password.</p>
      <SettingPasswordBox setValue={setPassword} buttonLabel='Change' />
    </div>
  );
};

const AccountSettingsBase = ({onClose}) => {
  const style = {
    width: 460,
    marginLeft: 'auto',
    marginRight: 'auto',
    backgroundColor: COLOR_VERY_LIGHT,
    padding: 10,
    paddingBottom: 20,
    border: "2px solid black",
    borderRadius: 10,

    zIndex: Z_INDEX_ACCOUNT_SETTINGS,
  };

  return <div style={style}>
    <HeaderBox onClose={onClose} />
    <ChangeEmailBox />
    <ChangePasswordBox />
  </div>;
};


const mapStateToProps = (state) => {
  return {
  };
};
const mapDispatchToProps = (dispatch) => {
  return {
    onClose: () => dispatch(accountSettingsToggle()),
  };
};

const AccountSettings = connect(mapStateToProps, mapDispatchToProps)(AccountSettingsBase);

export default AccountSettings;
