import React from 'react';
import { connect } from 'react-redux';
import {
  accountSettingsToggle,
  signOut,
  userMenuToggle,
} from '../actions';
import {
  COLOR_VERY_DARK,
  COLOR_VERY_LIGHT,

  SIZE_BUTTON_HEIGHT,

  Z_INDEX_ACCOUNT_MENU,
} from './style';
import IconButton from './iconbutton';

const Menu = ({visible, onShowSettings, onSignOut}) => {
  const menu_style = {
    display: visible ? 'inline-block' : 'none',
    position: 'absolute',
    top: SIZE_BUTTON_HEIGHT, right: 0,
    width: 120,

    zIndex: Z_INDEX_ACCOUNT_MENU,

    backgroundColor: COLOR_VERY_DARK,
    color: COLOR_VERY_LIGHT,
    textAlign: 'center',
    cursor: 'pointer',
  };

  return <div style={menu_style}>
    <p onClick={onShowSettings}>Account settings...</p>
    <hr />
    <p onClick={onSignOut}>Sign out</p>
  </div>;
};

const UserMenuBase = function({
  user,
  visible,
  onToggle,
  onShowSettings,
  onSignOut,
}) {
  let style = {};
  let img = "/user.opt.svg";
  if (visible) {
    style = Object.assign({}, style, {
      backgroundColor: COLOR_VERY_DARK,
      color: COLOR_VERY_LIGHT,
    });
    img = "/user-invert.opt.svg";
  }

  const onSignOutClick = () => onSignOut(user);

  const tip = null; //"View account settings";
  return <div style={style}>
    <IconButton
      tip={tip}
      tipPosition="bottomleft"
      icon={img}
      onClick={onToggle}
    />
    <Menu
      visible={visible}
      onShowSettings={onShowSettings}
      onSignOut={onSignOutClick}
    />
  </div>;
};

const mapStateToProps = (state) => {
  return {
    user: state.user,
    visible: state.user_menu.visible,
  };
};
const mapDispatchToProps = (dispatch) => {
  return {
    onToggle: () => dispatch(userMenuToggle()),
    onShowSettings: () => dispatch(accountSettingsToggle()),
    onSignOut: (user) => dispatch(signOut(user)),
  };
};
const UserMenu = connect(mapStateToProps, mapDispatchToProps)(UserMenuBase);

export default UserMenu;
